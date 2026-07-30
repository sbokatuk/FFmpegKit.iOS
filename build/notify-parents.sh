#!/usr/bin/env bash
set -euo pipefail

# Tells the parent repositories about a release they will want to re-pin.
#
# An umbrella pins this repository's packages by exact version, so a release here is invisible
# there until its daily upstream-drift check notices the pin is behind. This is the push half of
# that pair: release.yml calls it after publishing, and it files (or updates) one issue per row
# of build/parents.tsv in the repository that owns the pin, linking the release and its notes.
#
#   ./build/notify-parents.sh v3.12.1.4            # file the issues
#   ./build/notify-parents.sh v3.12.1.4 --dry-run  # print what would be filed, touch nothing
#
# Which parents care, and about which packages, lives in build/parents.tsv; this script only
# knows how to tell them. Rows whose tag prefix does not match the released tag are skipped, so
# a track without a parent notifies nobody - and so does a prerelease tag, whose suffix fails the
# version shape below: a parent is never pointed at a beta.
#
# Cross-repository issue writes cannot use GITHUB_TOKEN - it is scoped to the repository running
# the workflow - so in CI this authenticates with PARENT_ISSUE_PAT, a fine-grained token whose
# only reach is Issues read/write on the parent repositories. Locally any GH_TOKEN with the same
# reach works, and --dry-run needs no token at all.

TAG="${1:-}"
DRY="${2:-}"
[ -n "${TAG}" ] || { echo "usage: $0 <tag> [--dry-run]" >&2; exit 2; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${ROOT}/build/parents.tsv"
[ -f "${MANIFEST}" ] || { echo "no ${MANIFEST}; nothing to notify"; exit 0; }

# The repository doing the releasing: Actions' environment, or the origin remote when run by hand.
CHILD="${GITHUB_REPOSITORY:-}"
if [ -z "${CHILD}" ]; then
    CHILD="$(git -C "${ROOT}" remote get-url origin 2>/dev/null \
      | sed -E 's#^(git@github\.com:|https://github\.com/)##; s#\.git$##')"
fi
[ -n "${CHILD}" ] || { echo "error: cannot determine this repository's owner/name" >&2; exit 1; }

run() { # run <description> <gh issue subcommand...>
    if [ "${DRY}" = "--dry-run" ]; then
        shift
        printf 'dry-run: gh %s\n' "$*"
    else
        echo "    $1"; shift
        gh "$@"
    fi
}

matched=0
while IFS=$'\t' read -r prefix parent packages; do
    case "${prefix}" in ''|'#'*) continue ;; esac

    version="${TAG#"${prefix}"}"
    [ "${version}" != "${TAG}" ] || continue
    printf '%s' "${version}" | grep -Eq '^[0-9]+(\.[0-9]+)*$' || continue
    matched=$((matched + 1))

    # Some of these families name the note file after the tag, others after the version;
    # whichever exists in this checkout is the changelog for what just shipped.
    notes=""
    for f in "docs/release-notes/${TAG}.md" "docs/release-notes/${version}.md"; do
        [ -f "${ROOT}/${f}" ] && { notes="https://github.com/${CHILD}/blob/HEAD/${f}"; break; }
    done

    first="${packages%% *}"
    title="Update ${first} to ${version}"
    marker="<!-- notify-parents: ${CHILD} ${TAG} -->"

    body="$(mktemp)"
    {
        ids=""
        for p in ${packages}; do ids="${ids}\`${p}\`, "; done
        printf '%s published %s: %s at `%s`.\n\n' "${CHILD}" "${TAG}" "${ids%, }" "${version}"
        printf -- '- Release: https://github.com/%s/releases/tag/%s\n' "${CHILD}" "${TAG}"
        [ -z "${notes}" ] || printf -- '- Release notes: %s\n' "${notes}"
        printf -- '- The pin lives in this repository'"'"'s `Directory.Build.props`; until it moves, the daily upstream-drift check keeps reporting it behind.\n'
        printf '\n%s\n' "${marker}"
    } > "${body}"

    if [ "${DRY}" = "--dry-run" ]; then
        printf -- '--- %s <- "%s"\n' "${parent}" "${title}"
        cat "${body}"
    fi

    # One open issue per package line. The exact title standing open means this notification
    # already happened (a re-run); an older "Update <id> to ..." means this release supersedes
    # it, which is told on the same thread and moved into the title, rather than a second issue
    # per release piling up.
    #
    # The list API, not --search: search rides an eventually-consistent index, so an issue this
    # script filed seconds ago is invisible to it and a re-run files a duplicate (measured, not
    # theorised). The list endpoint is read-your-writes; the title match happens here instead.
    open_json="$([ "${DRY}" = "--dry-run" ] && printf '[]' \
      || gh issue list -R "${parent}" --state open -L 100 --json number,title \
      | jq --arg p "Update ${first} to " '[.[] | select(.title | startswith($p))]')"
    exact="$(printf '%s' "${open_json}" | jq -r --arg t "${title}" \
      '[.[] | select(.title == $t)][0].number // empty')"
    if [ -n "${exact}" ]; then
        seen="$(gh issue view "${exact}" -R "${parent}" --json body,comments \
          --jq '.body, .comments[].body')"
        case "${seen}" in *"${marker}"*)
            echo "    ${parent}#${exact} already told about ${TAG}"
            rm -f "${body}"
            continue
            ;;
        esac
        run "comment on ${parent}#${exact}" issue comment "${exact}" -R "${parent}" --body-file "${body}"
    else
        older="$(printf '%s' "${open_json}" | jq -r '.[0].number // empty')"
        if [ -n "${older}" ]; then
            run "comment on ${parent}#${older}" issue comment "${older}" -R "${parent}" --body-file "${body}"
            run "retitle ${parent}#${older}" issue edit "${older}" -R "${parent}" --title "${title}"
        else
            run "open issue in ${parent}" issue create -R "${parent}" --title "${title}" --body-file "${body}"
        fi
    fi
    rm -f "${body}"
done < "${MANIFEST}"

echo "notified for ${matched} matching row(s) of build/parents.tsv (tag ${TAG})"
