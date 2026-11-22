import cn.haiyu.powerwallet.EventDrivenGesture;
import java.util.*;

public class EventDrivenGestureTest {
    static final class Job implements EventDrivenGesture.Ticket {
        long due;Runnable task;boolean cancelled;
        public void cancel(){cancelled=true;}
    }
    static final class Harness implements EventDrivenGesture.Clock,EventDrivenGesture.Scheduler {
        long now;int launches,callbacks;List<Job> jobs=new ArrayList<>();
        EventDrivenGesture gesture=new EventDrivenGesture(this,this,() -> launches++);
        public long now(){return now;}
        public EventDrivenGesture.Ticket schedule(Runnable task,long delay){
            if(delay<0)throw new AssertionError("negative delay");
            Job job=new Job();job.due=now+delay;job.task=task;jobs.add(job);return job;
        }
        void advance(long target){
            while(true){Job next=null;for(Job job:jobs)if(!job.cancelled && job.due<=target && (next==null || job.due<next.due))next=job;
                if(next==null)break;jobs.remove(next);now=next.due;callbacks++;next.task.run();}
            now=target;
        }
        void key(boolean down,long timestamp){now=timestamp;gesture.event(down,timestamp);}
        void tap(long down,long up){key(true,down);key(false,up);}
        long pending(){return jobs.stream().filter(j -> !j.cancelled).count();}
    }
    static void check(boolean value,String name){if(!value)throw new AssertionError(name);}
    public static void main(String[] args){
        Harness h=new Harness();h.advance(3600000);check(h.callbacks==0 && h.pending()==0,"idle hour has no timer callbacks");
        h=new Harness();h.tap(100,160);h.advance(100000);check(h.callbacks==0 && h.pending()==0,"single tap schedules nothing");
        h=new Harness();h.tap(100,160);h.tap(300,360);check(h.pending()==1,"double tap arms one task");h.advance(679);check(h.launches==0,"quiet period retained");h.advance(680);check(h.launches==1 && h.pending()==0,"one launch at deadline");h.advance(100000);check(h.callbacks==1,"no periodic work after launch");
        h=new Harness();h.tap(100,150);h.tap(300,350);Job stale=h.jobs.get(0);h.tap(500,550);check(h.pending()==0,"third tap cancels task");stale.task.run();h.advance(2000);check(h.launches==0,"cancelled callback cannot launch even if delivered");
        h=new Harness();h.tap(100,150);h.tap(300,350);stale=h.jobs.get(0);h.tap(500,550);h.tap(2000,2050);h.tap(2200,2250);stale.task.run();check(h.launches==0,"old generation cannot consume new gesture");h.advance(2570);check(h.launches==1,"new gesture survives stale callback");
        h=new Harness();h.tap(100,150);h.tap(300,350);h.key(false,355);h.advance(670);check(h.launches==1,"duplicate release preserves original deadline");
        h=new Harness();h.tap(100,150);h.tap(300,350);stale=h.jobs.get(0);h.gesture.close();stale.task.run();h.tap(1000,1050);h.tap(1200,1250);h.advance(3000);check(h.launches==0 && h.pending()==0,"shutdown cancels work and ignores future input");
        h=new Harness();h.tap(100,500);h.tap(600,650);h.advance(2000);check(h.callbacks==0,"long press does not schedule a double tap");
        h=new Harness();for(int i=0;i<5;i++)h.tap(100+i*200,150+i*200);h.advance(2000);check(h.launches==0 && h.callbacks==0,"five tap sequence cancels before callback");
        h=new Harness();h.now=2000;h.gesture.event(true,100);h.gesture.event(false,150);h.gesture.event(true,300);h.gesture.event(false,350);h.advance(2000);check(h.launches==1,"late input delivery uses immediate nonnegative delay");
        h=new Harness();h.tap(100,150);h.tap(300,350);h.now=500;h.jobs.get(0).task.run();check(h.launches==0 && h.pending()>0,"early timer cannot launch before uptime deadline");h.advance(670);check(h.launches==1,"early timer re-arms for the remaining quiet period");
        System.out.println("11 event-driven scheduling cases passed");
    }
}
