package com.voidx.game;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.net.Uri;
import java.io.ByteArrayInputStream;

public final class MainActivity extends Activity {
  private WebView game;
  private void immersive() {
    getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY | View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
  }
  @Override public void onCreate(Bundle state) {
    super.onCreate(state);
    getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
    getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    if (android.os.Build.VERSION.SDK_INT >= 28) {
      WindowManager.LayoutParams params = getWindow().getAttributes();
      params.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
      getWindow().setAttributes(params);
    }
    game = new WebView(this);
    game.setBackgroundColor(0xff080808);
    WebSettings settings = game.getSettings();
    settings.setJavaScriptEnabled(true);
    settings.setDomStorageEnabled(true);
    settings.setAllowFileAccess(false);
    settings.setAllowContentAccess(false);
    settings.setMediaPlaybackRequiresUserGesture(true);
    settings.setSupportZoom(false);
    game.setWebViewClient(new WebViewClient() {
      @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
        Uri uri = request.getUrl();
        String p = uri.getPath();
        if (!"https".equals(uri.getScheme()) || !"voidx.local".equals(uri.getHost()) || p == null || p.contains("..")) return new WebResourceResponse("text/plain", "UTF-8", new ByteArrayInputStream(new byte[0]));
        if (p.equals("/")) p = "/index.html";
        String mime = p.endsWith(".js") ? "application/javascript" : p.endsWith(".css") ? "text/css" : p.endsWith(".png") ? "image/png" : "text/html";
        try { return new WebResourceResponse(mime, "UTF-8", getAssets().open("game" + p)); }
        catch (Exception e) { return new WebResourceResponse("text/plain", "UTF-8", new ByteArrayInputStream(new byte[0])); }
      }
      @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) { return !"voidx.local".equals(request.getUrl().getHost()); }
    });
    setContentView(game);
    immersive();
    game.loadUrl("https://voidx.local/index.html");
  }
  @Override public void onWindowFocusChanged(boolean focus) { super.onWindowFocusChanged(focus); if (focus) immersive(); }
  @Override protected void onPause() { if(game != null) {game.evaluateJavascript("window.VoidX && window.VoidX.pause()", null); game.onPause();} super.onPause(); }
  @Override protected void onResume() { super.onResume(); if (game != null) game.onResume(); immersive(); }
  @Override public void onBackPressed() { if(game != null) game.evaluateJavascript("window.VoidX && window.VoidX.back()", null); }
  @Override protected void onDestroy() { if (game != null) {game.destroy(); game = null;} super.onDestroy(); }
}
