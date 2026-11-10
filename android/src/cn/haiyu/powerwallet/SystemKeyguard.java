package cn.haiyu.powerwallet;

import android.os.Bundle;
import android.os.IBinder;
import java.lang.reflect.Method;

/** Same system Binder queries used by this ROM's KeyguardManager; no APK process needed. */
final class SystemKeyguard {
    private final Method showingMethod, lockedMethod;
    private Object window, trust;

    SystemKeyguard() throws Exception {
        showingMethod=Class.forName("android.view.IWindowManager").getMethod("isKeyguardLocked");
        lockedMethod=Class.forName("android.app.trust.ITrustManager").getMethod("isDeviceLocked",int.class,int.class);
        connect();
    }
    private void connect() throws Exception {
        Method service=Class.forName("android.os.ServiceManager").getMethod("getService",String.class);
        IBinder windowBinder=(IBinder)service.invoke(null,"window");
        IBinder trustBinder=(IBinder)service.invoke(null,"trust");
        if(windowBinder==null || trustBinder==null)throw new IllegalStateException("System keyguard services unavailable");
        window=Class.forName("android.view.IWindowManager$Stub").getMethod("asInterface",IBinder.class).invoke(null,windowBinder);
        trust=Class.forName("android.app.trust.ITrustManager$Stub").getMethod("asInterface",IBinder.class).invoke(null,trustBinder);
    }
    synchronized Bundle read() throws Exception {
        try{return query();}
        catch(Exception disconnected){connect();return query();}
    }
    private Bundle query() throws Exception {
        Bundle state=new Bundle();
        state.putBoolean("showing",(Boolean)showingMethod.invoke(window));
        state.putBoolean("deviceLocked",(Boolean)lockedMethod.invoke(trust,0,0));
        return state;
    }
}
