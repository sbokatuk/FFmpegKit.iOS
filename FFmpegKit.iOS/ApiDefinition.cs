using System;
using CoreFoundation;
using ObjCRuntime;
using Foundation;
using UIKit;

namespace Ffmpegkit.Ios
{
    // The first step to creating a binding is to add your native library ("libNativeLibrary.a")
    // to the project by right-clicking (or Control-clicking) the folder containing this source
    // file and clicking "Add files..." and then simply select the native library (or libraries)
    // that you want to bind.
    //
    // When you do that, you'll notice that MonoDevelop generates a code-behind file for each
    // native library which will contain a [LinkWith] attribute. VisualStudio auto-detects the
    // architectures that the native library supports and fills in that information for you,
    // however, it cannot auto-detect any Frameworks or other system libraries that the
    // native library may depend on, so you'll need to fill in that information yourself.
    //
    // Once you've done that, you're ready to move on to binding the API...
    //
    //
    // Here is where you'd define your API definition for the native Objective-C library.
    //
    // For example, to bind the following Objective-C class:
    //
    //     @interface Widget : NSObject {
    //     }
    //
    // The C# binding would look like this:
    //
    //     [BaseType (typeof (NSObject))]
    //     interface Widget {
    //     }
    //
    // To bind Objective-C properties, such as:
    //
    //     @property (nonatomic, readwrite, assign) CGPoint center;
    //
    // You would add a property definition in the C# interface like so:
    //
    //     [Export ("center")]
    //     CGPoint Center { get; set; }
    //
    // To bind an Objective-C method, such as:
    //
    //     -(void) doSomething:(NSObject *)object atIndex:(NSInteger)index;
    //
    // You would add a method definition to the C# interface like so:
    //
    //     [Export ("doSomething:atIndex:")]
    //     void DoSomething (NSObject object, int index);
    //
    // Objective-C "constructors" such as:
    //
    //     -(id)initWithElmo:(ElmoMuppet *)elmo;
    //
    // Can be bound as:
    //
    //     [Export ("initWithElmo:")]
    //     IntPtr Constructor (ElmoMuppet elmo);
    //
    // For more information, see https://aka.ms/ios-binding
    //

    // @interface Log : NSObject
    [BaseType(typeof(NSObject))]
    partial interface Log
    {
        // -(long)getSessionId;
        [Export("getSessionId")]
        nint SessionId { get; }

        // -(int)getLevel;
        [Export("getLevel")]
        int Level { get; }

        // -(NSString *)getMessage;
        [Export("getMessage")]
        string Message { get; }
    }

    // typedef void (^LogCallback)(Log *);
    delegate void LogCallback(Log arg0);

    // @interface ReturnCode : NSObject
    [BaseType(typeof(NSObject))]
    interface ReturnCode
    {
        // +(BOOL)isSuccess:(ReturnCode *)value;
        [Static]
        [Export("isSuccess:")]
        bool IsSuccess(ReturnCode value);

        // +(BOOL)isCancel:(ReturnCode *)value;
        [Static]
        [Export("isCancel:")]
        bool IsCancel(ReturnCode value);

        // -(int)getValue;
        [Export("getValue")]
        int Value { get; }

        // -(BOOL)isValueSuccess;
        [Export("isValueSuccess")]
        bool IsValueSuccess { get; }

        // -(BOOL)isValueError;
        [Export("isValueError")]
        bool IsValueError { get; }

        // -(BOOL)isValueCancel;
        [Export("isValueCancel")]
        bool IsValueCancel { get; }
    }

    // @protocol Session
    /*
	  Check whether adding [Model] to this declaration is appropriate.
	  [Model] is used to generate a C# class that implements this protocol,
	  and might be useful for protocols that consumers are supposed to implement,
	  since consumers can subclass the generated class instead of implementing
	  the generated interface. If consumers are not supposed to implement this
	  protocol, then [Model] is redundant and will generate code that will never
	  be used.
	*/
    [Protocol]
    [BaseType(typeof(NSObject))]
    interface Session
    {
        // @required -(LogCallback)getLogCallback;
        [Abstract]
        [Export("getLogCallback")]
        LogCallback LogCallback { get; }

        // @required -(long)getSessionId;
        [Abstract]
        [Export("getSessionId")]
        nint SessionId { get; }

        // @required -(NSDate *)getCreateTime;
        [Abstract]
        [Export("getCreateTime")]
        NSDate CreateTime { get; }

        // @required -(NSDate *)getStartTime;
        [Abstract]
        [Export("getStartTime")]
        NSDate StartTime { get; }

        // @required -(NSDate *)getEndTime;
        [Abstract]
        [Export("getEndTime")]
        NSDate EndTime { get; }

        // @required -(long)getDuration;
        [Abstract]
        [Export("getDuration")]
        nint Duration { get; }

        // @required -(NSArray *)getArguments;
        [Abstract]
        [Export("getArguments")]
        NSObject[] Arguments { get; }

        // @required -(NSString *)getCommand;
        [Abstract]
        [Export("getCommand")]
        string Command { get; }

        //// @required -(NSArray *)getAllLogsWithTimeout:(int)waitTimeout;
        //[Abstract]
        //[Export("getAllLogsWithTimeout:")]
        //NSObject[] GetAllLogsWithTimeout(int waitTimeout);

        // @required -(NSArray *)getAllLogs;
        [Abstract]
        [Export("getAllLogs")]
        NSObject[] AllLogs { get; }

        // @required -(NSArray *)getLogs;
        [Abstract]
        [Export("getLogs")]
        NSObject[] Logs { get; }

        //// @required -(NSString *)getAllLogsAsStringWithTimeout:(int)waitTimeout;
        //[Abstract]
        //[Export("getAllLogsAsStringWithTimeout:")]
        //string GetAllLogsAsStringWithTimeout(int waitTimeout);

        // @required -(NSString *)getAllLogsAsString;
        [Abstract]
        [Export("getAllLogsAsString")]
        string AllLogsAsString { get; }

        // @required -(NSString *)getLogsAsString;
        [Abstract]
        [Export("getLogsAsString")]
        string LogsAsString { get; }

        // @required -(NSString *)getOutput;
        [Abstract]
        [Export("getOutput")]
        string Output { get; }

        // @required -(SessionState)getState;
        [Abstract]
        [Export("getState")]
        SessionState State();

        // @required -(ReturnCode *)getReturnCode;
        [Abstract]
        [Export("getReturnCode")]
        ReturnCode ReturnCode();

        // @required -(NSString *)getFailStackTrace;
        [Abstract]
        [Export("getFailStackTrace")]
        string FailStackTrace { get; }

        // @required -(LogRedirectionStrategy)getLogRedirectionStrategy;
        [Abstract]
        [Export("getLogRedirectionStrategy")]
        LogRedirectionStrategy LogRedirectionStrategy();

        // @required -(BOOL)thereAreAsynchronousMessagesInTransmit;
        [Abstract]
        [Export("thereAreAsynchronousMessagesInTransmit")]
        bool ThereAreAsynchronousMessagesInTransmit();

        // @required -(void)addLog:(Log *)log;
        [Abstract]
        [Export("addLog:")]
        void AddLog(Log log);

        // @required -(void)startRunning;
        [Abstract]
        [Export("startRunning")]
        void StartRunning();

        // @required -(void)complete:(ReturnCode *)returnCode;
        [Abstract]
        [Export("complete:")]
        void Complete(ReturnCode returnCode);

        // @required -(void)fail:(NSException *)exception;
        [Abstract]
        [Export("fail:")]
        void Fail(NSException exception);

        // @required -(BOOL)isFFmpeg;
        [Abstract]
        [Export("isFFmpeg")]
        bool IsFFmpeg { get; }

        // @required -(BOOL)isFFprobe;
        [Abstract]
        [Export("isFFprobe")]
        bool IsFFprobe { get; }

        // @required -(BOOL)isMediaInformation;
        [Abstract]
        [Export("isMediaInformation")]
        bool IsMediaInformation { get; }

        // @required -(void)cancel;
        [Abstract]
        [Export("cancel")]
        void Cancel();
    }

    //[Static]
    //partial interface Constants
    //{
    //    // extern const int AbstractSessionDefaultTimeoutForAsynchronousMessagesInTransmit;
    //    [Field("AbstractSessionDefaultTimeoutForAsynchronousMessagesInTransmit", "__Internal")]
    //    int AbstractSessionDefaultTimeoutForAsynchronousMessagesInTransmit { get; }
    //}

    // @interface AbstractSession : NSObject <Session>
    [BaseType(typeof(NSObject))]
    interface AbstractSession : Session
    {
        // -(instancetype)init:(NSArray *)arguments withLogCallback:(LogCallback)logCallback withLogRedirectionStrategy:(LogRedirectionStrategy)logRedirectionStrategy;
        [Export("init:withLogCallback:withLogRedirectionStrategy:")]
        IntPtr Constructor(NSObject[] arguments, LogCallback logCallback, LogRedirectionStrategy logRedirectionStrategy);

        // -(void)waitForAsynchronousMessagesInTransmit:(int)timeout;
        [Export("waitForAsynchronousMessagesInTransmit:")]
        void WaitForAsynchronousMessagesInTransmit(int timeout);
    }

    // @interface Statistics : NSObject
    [BaseType(typeof(NSObject))]
    interface Statistics
    {
        // -(instancetype)init:(long)sessionId videoFrameNumber:(int)videoFrameNumber videoFps:(float)videoFps videoQuality:(float)videoQuality size:(int64_t)size time:(double)time bitrate:(double)bitrate speed:(double)speed;
        [Export("init:videoFrameNumber:videoFps:videoQuality:size:time:bitrate:speed:")]
        IntPtr Constructor(nint sessionId, int videoFrameNumber, float videoFps, float videoQuality, long size, double time, double bitrate, double speed);

        // -(long)getSessionId;
        [Export("getSessionId")]
        nint SessionId { get; }

        // -(int)getVideoFrameNumber;
        [Export("getVideoFrameNumber")]
        int VideoFrameNumber { get; }

        // -(float)getVideoFps;
        [Export("getVideoFps")]
        float VideoFps { get; }

        // -(float)getVideoQuality;
        [Export("getVideoQuality")]
        float VideoQuality { get; }

        // -(long)getSize;
        [Export("getSize")]
        nint Size { get; }

        // -(double)getTime;
        [Export("getTime")]
        double Time { get; }

        // -(double)getBitrate;
        [Export("getBitrate")]
        double Bitrate { get; }

        // -(double)getSpeed;
        [Export("getSpeed")]
        double Speed { get; }
    }

    // typedef void (^StatisticsCallback)(Statistics *);
    delegate void StatisticsCallback(Statistics arg0);

    // typedef void (^FFmpegSessionCompleteCallback)(FFmpegSession *);
    delegate void FFmpegSessionCompleteCallback(FFmpegSession arg0);

    // @interface FFmpegSession : AbstractSession
    [BaseType(typeof(AbstractSession))]
    interface FFmpegSession
    {
        // +(instancetype)create:(NSArray *)arguments;
        [Static]
        [Export("create:")]
        FFmpegSession Create(NSObject[] arguments);

        // +(instancetype)create:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback;
        [Static]
        [Export("create:withCompleteCallback:")]
        FFmpegSession Create(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback);

        // +(instancetype)create:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback;
        [Static]
        [Export("create:withCompleteCallback:withLogCallback:withStatisticsCallback:")]
        FFmpegSession Create(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback);

        // +(instancetype)create:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback withLogRedirectionStrategy:(LogRedirectionStrategy)logRedirectionStrategy;
        [Static]
        [Export("create:withCompleteCallback:withLogCallback:withStatisticsCallback:withLogRedirectionStrategy:")]
        FFmpegSession Create(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback, LogRedirectionStrategy logRedirectionStrategy);

        // -(StatisticsCallback)getStatisticsCallback;
        [Export("getStatisticsCallback")]
        StatisticsCallback StatisticsCallback { get; }

        // -(FFmpegSessionCompleteCallback)getCompleteCallback;
        [Export("getCompleteCallback")]
        FFmpegSessionCompleteCallback CompleteCallback { get; }

        // -(NSArray *)getAllStatisticsWithTimeout:(int)waitTimeout;
        [Export("getAllStatisticsWithTimeout:")]
        NSObject[] GetAllStatisticsWithTimeout(int waitTimeout);

        // -(NSArray *)getAllStatistics;
        [Export("getAllStatistics")]
        NSObject[] AllStatistics { get; }

        // -(NSArray *)getStatistics;
        [Export("getStatistics")]
        NSObject[] Statistics { get; }

        // -(Statistics *)getLastReceivedStatistics;
        [Export("getLastReceivedStatistics")]
        Statistics LastReceivedStatistics { get; }

        // -(void)addStatistics:(Statistics *)statistics;
        [Export("addStatistics:")]
        void AddStatistics(Statistics statistics);
    }

    // @interface FFmpegKit : NSObject
    [BaseType(typeof(NSObject))]
    interface FFmpegKit
    {
        // +(FFmpegSession *)executeWithArguments:(NSArray *)arguments;
        [Static]
        [Export("executeWithArguments:")]
        FFmpegSession ExecuteWithArguments(NSObject[] arguments);

        // +(FFmpegSession *)executeWithArgumentsAsync:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback;
        [Static]
        [Export("executeWithArgumentsAsync:withCompleteCallback:")]
        FFmpegSession ExecuteWithArgumentsAsync(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback);

        // +(FFmpegSession *)executeWithArgumentsAsync:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback;
        [Static]
        [Export("executeWithArgumentsAsync:withCompleteCallback:withLogCallback:withStatisticsCallback:")]
        FFmpegSession ExecuteWithArgumentsAsync(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback);

        // +(FFmpegSession *)executeWithArgumentsAsync:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback onDispatchQueue:(dispatch_queue_t)queue;
        [Static]
        [Export("executeWithArgumentsAsync:withCompleteCallback:onDispatchQueue:")]
        FFmpegSession ExecuteWithArgumentsAsync(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback, DispatchQueue queue);

        // +(FFmpegSession *)executeWithArgumentsAsync:(NSArray *)arguments withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback onDispatchQueue:(dispatch_queue_t)queue;
        [Static]
        [Export("executeWithArgumentsAsync:withCompleteCallback:withLogCallback:withStatisticsCallback:onDispatchQueue:")]
        FFmpegSession ExecuteWithArgumentsAsync(NSObject[] arguments, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback, DispatchQueue queue);

        // +(FFmpegSession *)execute:(NSString *)command;
        [Static]
        [Export("execute:")]
        FFmpegSession Execute(string command);

        // +(FFmpegSession *)executeAsync:(NSString *)command withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback;
        [Static]
        [Export("executeAsync:withCompleteCallback:")]
        FFmpegSession ExecuteAsync(string command, FFmpegSessionCompleteCallback completeCallback);

        // +(FFmpegSession *)executeAsync:(NSString *)command withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback;
        [Static]
        [Export("executeAsync:withCompleteCallback:withLogCallback:withStatisticsCallback:")]
        FFmpegSession ExecuteAsync(string command, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback);

        // +(FFmpegSession *)executeAsync:(NSString *)command withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback onDispatchQueue:(dispatch_queue_t)queue;
        [Static]
        [Export("executeAsync:withCompleteCallback:onDispatchQueue:")]
        FFmpegSession ExecuteAsync(string command, FFmpegSessionCompleteCallback completeCallback, DispatchQueue queue);

        // +(FFmpegSession *)executeAsync:(NSString *)command withCompleteCallback:(FFmpegSessionCompleteCallback)completeCallback withLogCallback:(LogCallback)logCallback withStatisticsCallback:(StatisticsCallback)statisticsCallback onDispatchQueue:(dispatch_queue_t)queue;
        [Static]
        [Export("executeAsync:withCompleteCallback:withLogCallback:withStatisticsCallback:onDispatchQueue:")]
        FFmpegSession ExecuteAsync(string command, FFmpegSessionCompleteCallback completeCallback, LogCallback logCallback, StatisticsCallback statisticsCallback, DispatchQueue queue);

        // +(void)cancel;
        [Static]
        [Export("cancel")]
        void Cancel();

        // +(void)cancel:(long)sessionId;
        [Static]
        [Export("cancel:")]
        void Cancel(nint sessionId);

        // +(NSArray *)listSessions;
        [Static]
        [Export("listSessions")]
        NSObject[] ListSessions { get; }
    }
}

