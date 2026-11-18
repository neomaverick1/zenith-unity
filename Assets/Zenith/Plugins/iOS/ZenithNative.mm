// Haptics and the share sheet on iPhone.
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController();

extern "C" void ZenithHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind)
        {
            case 6: { // win
                UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
                [g notificationOccurred:UINotificationFeedbackTypeSuccess];
                break;
            }
            case 7: { // lose
                UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
                [g notificationOccurred:UINotificationFeedbackTypeWarning];
                break;
            }
            default: {
                UIImpactFeedbackStyle style = UIImpactFeedbackStyleLight;
                if (kind == 2) style = UIImpactFeedbackStyleMedium;          // merge
                if (kind == 3 || kind == 4) style = UIImpactFeedbackStyleHeavy; // big, huge
                if (kind == 0) style = UIImpactFeedbackStyleSoft;              // tap
                UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
                [g impactOccurred];
                if (kind == 4) {
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.12 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                        UIImpactFeedbackGenerator *h = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
                        [h impactOccurred];
                    });
                }
                break;
            }
        }
    });
}

extern "C" void ZenithShare(const char *text, const char *url)
{
    NSString *t = [NSString stringWithUTF8String:text ? text : ""];
    NSString *u = [NSString stringWithUTF8String:url ? url : ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray *items = [NSMutableArray arrayWithObject:t];
        NSURL *link = [NSURL URLWithString:u];
        if (link) [items addObject:link];
        UIActivityViewController *share = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController *root = UnityGetGLViewController();
        share.popoverPresentationController.sourceView = root.view;
        share.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 1, 1);
        [root presentViewController:share animated:YES completion:nil];
    });
}
