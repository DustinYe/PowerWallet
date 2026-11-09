import cn.haiyu.powerwallet.PressDetector;

public class PressDetectorTest {
    private static void check(boolean value, String name) {
        if (!value) throw new AssertionError(name);
    }
    private static void tap(PressDetector d, long down, long up) { d.event(true, down); d.event(false, up); }
    public static void main(String[] args) {
        PressDetector d = new PressDetector();
        tap(d, 100, 160); check(!d.poll(1000), "single tap");
        d = new PressDetector(); tap(d, 100, 160); tap(d, 300, 360);
        check(!d.poll(679), "quiet period"); check(d.poll(680), "double tap"); check(!d.poll(999), "once only");
        d = new PressDetector(); tap(d, 100, 600); tap(d, 700, 750); check(!d.poll(1200), "hold then tap");
        d = new PressDetector(); tap(d, 100, 150); tap(d, 300, 700); check(!d.poll(1200), "tap then hold");
        d = new PressDetector(); tap(d, 100, 150); tap(d, 300, 350); tap(d, 500, 550); check(!d.poll(1200), "triple cancels");
        d = new PressDetector(); for(int i=0;i<5;i++) tap(d,100+200*i,150+200*i); check(!d.poll(2000), "SOS unaffected");
        d = new PressDetector(); tap(d, 100, 150); tap(d, 501, 550); check(!d.poll(1000), "slow taps");
        d = new PressDetector(); d.event(false, 10); d.event(true, 100); d.event(true, 120); d.event(false, 150); tap(d, 300, 350); check(d.poll(670), "duplicate and stray events");
        d = new PressDetector(); tap(d,100,150); tap(d,300,350); tap(d,500,550); tap(d,2000,2050); tap(d,2200,2250); check(d.poll(2570), "recovery after triple");
        System.out.println("9 gesture cases passed");
    }
}
