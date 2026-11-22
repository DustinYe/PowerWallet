package cn.haiyu.powerwallet;

/** Schedules work only after a completed double tap; no periodic idle polling. */
public final class EventDrivenGesture implements AutoCloseable {
    public interface Clock { long now(); }
    public interface Ticket { void cancel(); }
    public interface Scheduler { Ticket schedule(Runnable task,long delayMillis); }
    private final PressDetector detector=new PressDetector();
    private final Clock clock;
    private final Scheduler scheduler;
    private final Runnable action;
    private Ticket ticket;
    private long generation;
    private boolean closed;

    public EventDrivenGesture(Clock clock,Scheduler scheduler,Runnable action) {
        this.clock=clock;this.scheduler=scheduler;this.action=action;
    }
    public synchronized void event(boolean pressed,long timestamp) {
        if(closed)return;
        detector.event(pressed,timestamp);
        arm();
    }
    private void arm() {
        if(ticket!=null){ticket.cancel();ticket=null;}
        final long token=++generation;
        long deadline=detector.deadline();
        if(deadline>=0)ticket=scheduler.schedule(() -> fire(token),Math.max(0,deadline-clock.now()));
    }
    private void fire(long token) {
        boolean launch;
        synchronized(this) {
            if(closed || generation!=token)return;
            ticket=null;
            launch=detector.poll(clock.now());
            if(!launch)arm(); // Defend against a scheduler firing before the uptime deadline.
        }
        if(launch)action.run();
    }
    @Override public synchronized void close() {
        closed=true;generation++;
        if(ticket!=null){ticket.cancel();ticket=null;}
    }
}
