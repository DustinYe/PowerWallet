package cn.haiyu.powerwallet;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.IBinder;
import android.os.Parcel;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.io.*;

public class MainActivity extends Activity {
    public static final String SOCKET = "cn.haiyu.powerwallet.v1";
    public static final String WALLET = "com.google.android.apps.walletnfcrel";
    private TextView status;
    private boolean busy;

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        getWindow().setStatusBarColor(Color.rgb(244,247,242));
        getWindow().setNavigationBarColor(Color.rgb(244,247,242));
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true); scroll.setBackgroundColor(Color.rgb(244,247,242));
        LinearLayout root = new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(28), dp(48), dp(28), dp(36)); scroll.addView(root);
        text(root, "POWER × 2", 13, true, 0xFF466E5A);
        text(root, "电源键钱包", 32, true, 0xFF173B32);
        TextView intro = text(root, "双击电源键，打开 Google 钱包。", 17, false, 0xFF446052);
        intro.setPadding(0, dp(12), 0, dp(28));
        status = text(root, "正在检查…", 16, true, 0xFF173B32);
        status.setPadding(dp(20), dp(20), dp(20), dp(20));
        GradientDrawable bg = new GradientDrawable(); bg.setColor(0xFFE0EDE2); bg.setCornerRadius(dp(18)); status.setBackground(bg);
        button(root, "打开 Google 钱包", new View.OnClickListener() { public void onClick(View v) { openWallet(); }});
        button(root, "刷新激活状态", new View.OnClickListener() { public void onClick(View v) { request("STATUS"); }});
        button(root, "停用并恢复原快捷键", new View.OnClickListener() { public void onClick(View v) {
            new AlertDialog.Builder(MainActivity.this).setTitle("恢复原快捷键？")
                .setMessage("停止按键助手，并恢复激活前的双击电源键设置。")
                .setPositiveButton("恢复", (d,w) -> request("STOP")).setNegativeButton("取消", null).show();
        }});
        TextView help = text(root, "使用说明\n\n激活后可拔掉数据线。手机重启后，需要连接电脑重新运行“激活.ps1”。\n\n锁屏时请按 Google 钱包要求完成指纹或密码验证。插件不读取银行卡，也不处理付款。\n\n卸载前请先点“停用并恢复原快捷键”。", 14, false, 0xFF536357);
        help.setPadding(0, dp(26), 0, 0);
        setContentView(scroll);
    }
    @Override protected void onResume() { super.onResume(); request("STATUS"); }
    private int dp(int n) { return Math.round(n * getResources().getDisplayMetrics().density); }
    private TextView text(LinearLayout root, String value, int size, boolean bold, int color) {
        TextView t = new TextView(this); t.setText(value); t.setTextSize(size); t.setTextColor(color);
        t.setLineSpacing(dp(3), 1); if (bold) t.setTypeface(null, Typeface.BOLD); root.addView(t); return t;
    }
    private void button(LinearLayout root, String label, View.OnClickListener listener) {
        Button b = new Button(this); b.setText(label); b.setAllCaps(false); b.setTextColor(0xFF173B32);
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, dp(56)); p.topMargin = dp(12);
        root.addView(b,p); b.setOnClickListener(listener);
    }
    private void openWallet() {
        try { startActivity(new Intent("com.google.android.apps.wallet.main.QUICKDRAW").setPackage(WALLET)); }
        catch (Exception first) {
            try { Intent i = getPackageManager().getLaunchIntentForPackage(WALLET); if(i == null) throw first; startActivity(i); }
            catch(Exception e) { new AlertDialog.Builder(this).setMessage("无法打开 Google 钱包，请检查是否已经安装并启用。").setPositiveButton("知道了",null).show(); }
        }
    }
    private void request(final String command) {
        if(busy) return; busy = true;
        new Thread(() -> {
            String reply;
            try {
                IBinder helper=BridgeProvider.helper;
                if(helper==null || !helper.isBinderAlive()) throw new IOException();
                Parcel data=Parcel.obtain(), response=Parcel.obtain();
                try {
                    data.writeInterfaceToken("cn.haiyu.powerwallet.Control");
                    if(!helper.transact("STOP".equals(command)?2:1,data,response,0)) throw new IOException();
                    response.readException(); reply=response.readString();
                    if(reply==null) throw new IOException();
                } finally {data.recycle();response.recycle();}
            } catch(Exception e) { reply = "未激活\n请连接电脑运行“激活.ps1”。"; }
            final String result = reply.replace("|", "\n");
            runOnUiThread(() -> { busy = false; status.setText(result); });
        }, "wallet-status").start();
    }
}
