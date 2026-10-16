#import <AppKit/AppKit.h>

static id magnifyMonitor;
static double accumulatedMagnification;

extern "C" int CBStartMagnify(void) {
    if (!magnifyMonitor) {
        magnifyMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskMagnify handler:^NSEvent *(NSEvent *event) {
            accumulatedMagnification += event.magnification;
            return event;
        }];
    }
    return magnifyMonitor ? 1 : 0;
}
extern "C" double CBConsumeMagnify(void) {
    double delta = accumulatedMagnification;
    accumulatedMagnification = 0;
    return delta;
}
extern "C" void CBStopMagnify(void) {
    if (magnifyMonitor) [NSEvent removeMonitor:magnifyMonitor];
    magnifyMonitor = nil;
    accumulatedMagnification = 0;
}
