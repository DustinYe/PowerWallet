package cn.haiyu.powerwallet;

/** Only two completed short taps count; further taps cancel the pending launch. */
public final class PressDetector {
    public static final long DOUBLE_MS = 400, HOLD_MS = 300, QUIET_MS = 320;
    private long down = -1, firstDown = -1, pending = -1, sequenceEnd = -1;
    private int taps = 0;

    public synchronized void event(boolean pressed, long time) {
        if (pressed) {
            if (down >= 0) return;
            if (sequenceEnd >= 0 && time - sequenceEnd > DOUBLE_MS) {
                taps = 0; firstDown = -1;
            }
            pending = -1;
            down = time;
            if (taps == 0) firstDown = time;
        } else {
            if (down < 0) return;
            long duration = time - down;
            if (duration < 0 || duration >= HOLD_MS) {
                reset(); return;
            }
            if (taps == 1 && down - firstDown > DOUBLE_MS) {
                taps = 0; firstDown = down;
            }
            taps++;
            sequenceEnd = time;
            if (taps == 2) pending = time + QUIET_MS;
            down = -1;
        }
    }

    public synchronized boolean poll(long now) {
        if (pending >= 0 && now >= pending && down < 0 && taps == 2) {
            reset(); return true;
        }
        return false;
    }

    private void reset() { down = firstDown = pending = sequenceEnd = -1; taps = 0; }
}
