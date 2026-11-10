package cn.haiyu.powerwallet;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.net.Uri;
import android.os.Binder;
import android.os.Bundle;
import android.os.IBinder;

/** Only ADB shell can deliver the narrowly scoped status/stop Binder. */
public final class BridgeProvider extends ContentProvider {
    public static volatile IBinder helper;
    @Override public boolean onCreate() { return true; }
    @Override public Bundle call(String method, String arg, Bundle extras) {
        if(Binder.getCallingUid()!=2000) throw new SecurityException("Shell only");
        if(!"bind".equals(method) || extras==null) throw new IllegalArgumentException();
        helper=extras.getBinder("helper");
        Bundle response=new Bundle(); response.putBoolean("ok",helper!=null); return response;
    }
    @Override public Cursor query(Uri u,String[] p,String s,String[] a,String o) {return null;}
    @Override public String getType(Uri u) {return null;}
    @Override public Uri insert(Uri u,ContentValues v) {throw new UnsupportedOperationException();}
    @Override public int delete(Uri u,String s,String[] a) {throw new UnsupportedOperationException();}
    @Override public int update(Uri u,ContentValues v,String s,String[] a) {throw new UnsupportedOperationException();}
}
