package com.voidx.auth;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.CancellationSignal;
import android.util.Base64;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.google.api.client.googleapis.auth.oauth2.GoogleIdToken;
import com.google.api.client.googleapis.auth.oauth2.GoogleIdTokenVerifier;
import com.google.api.client.http.javanet.NetHttpTransport;
import com.google.api.client.json.gson.GsonFactory;

import org.json.JSONObject;
import java.io.IOException;
import java.lang.ref.WeakReference;
import java.security.SecureRandom;
import java.util.Collections;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Direct Google identity for the offline player profile. No Firebase and no client secret. */
public final class GoogleAuthBridge {
    public interface Callback { void onResult(String json); }
    private static WeakReference<Activity> owner = new WeakReference<>(null);
    private static Callback callback;
    private static CredentialManager manager;
    private static SharedPreferences preferences;
    private static GoogleIdTokenVerifier verifier;
    private static String clientId;
    private static CancellationSignal pending;
    private static int generation;
    private static final ExecutorService verifierWorker = Executors.newSingleThreadExecutor();

    public static void initialize(Activity activity, String webClientId, Callback listener) {
        activity.runOnUiThread(() -> {
            generation++;
            if (pending != null) pending.cancel();
            pending = null;
            owner = new WeakReference<>(activity); callback = listener; clientId = webClientId;
            manager = CredentialManager.create(activity.getApplicationContext());
            preferences = activity.getSharedPreferences("voidx.google", Context.MODE_PRIVATE);
            verifier = new GoogleIdTokenVerifier.Builder(new NetHttpTransport(), GsonFactory.getDefaultInstance())
                    .setAudience(Collections.singletonList(clientId))
                    .build();
            send("guest", "", "", "", "");
            if (preferences.getBoolean("restore", false)) request(true);
        });
    }

    public static void signIn() {
        Activity activity = owner.get();
        if (activity != null) activity.runOnUiThread(() -> request(false));
    }

    private static void request(boolean restoring) {
        Activity activity = owner.get();
        if (activity == null || activity.isFinishing() || pending != null) return;
        if (clientId == null || !clientId.endsWith(".apps.googleusercontent.com")) {
            send("error", "configuration", "", "", ""); return;
        }
        final int attempt = ++generation;
        byte[] bytes = new byte[32]; new SecureRandom().nextBytes(bytes);
        final String nonce = Base64.encodeToString(bytes, Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
        final GoogleIdTokenVerifier currentVerifier = verifier;
        GetCredentialRequest.Builder request = new GetCredentialRequest.Builder();
        if (restoring) {
            request.addCredentialOption(new GetGoogleIdOption.Builder().setServerClientId(clientId)
                    .setFilterByAuthorizedAccounts(true).setAutoSelectEnabled(true).setNonce(nonce).build());
            request.setPreferImmediatelyAvailableCredentials(true);
        } else {
            request.addCredentialOption(new GetSignInWithGoogleOption.Builder(clientId).setNonce(nonce).build());
        }
        pending = new CancellationSignal();
        send("busy", "", "", "", "");
        try {
            manager.getCredentialAsync(activity, request.build(), pending, activity.getMainExecutor(),
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                @Override public void onResult(GetCredentialResponse response) {
                    if (attempt != generation) return;
                    try {
                        Credential credential = response.getCredential();
                        if (!(credential instanceof CustomCredential) ||
                                !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            finish(attempt, "error", "verification", "", "", ""); return;
                        }
                        String token = GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData()).getIdToken();
                        // Validate Google's signature, audience, issuer and expiry using Google's library.
                        // Only the verified profile crosses JNI; tokens are never logged or stored by VoidX.
                        verifierWorker.execute(() -> {
                            try {
                                GoogleIdToken verified = currentVerifier.verify(token);
                                if (verified == null || !nonce.equals(verified.getPayload().get("nonce")) ||
                                        verified.getPayload().getSubject() == null) {
                                    finish(attempt, "error", "verification", "", "", ""); return;
                                }
                                GoogleIdToken.Payload profile = verified.getPayload();
                                String name = profile.get("name") instanceof String ? (String) profile.get("name") : "Игрок";
                                finish(attempt, "signedIn", "", profile.getSubject(), name, profile.getEmail());
                            } catch (IOException network) { finish(attempt, "error", "network", "", "", ""); }
                            catch (Exception invalid) { finish(attempt, "error", "verification", "", "", ""); }
                        });
                    } catch (Exception invalid) { finish(attempt, "error", "verification", "", "", ""); }
                }
                @Override public void onError(GetCredentialException error) {
                    android.util.Log.w("VoidXAuth", "Credential Manager: " + error.getType());
                    String code = error instanceof GetCredentialCancellationException ? "cancelled" :
                            error instanceof NoCredentialException ? "no_account" : "provider";
                    finish(attempt, restoring ? "guest" : "error", code, "", "", "");
                }
            });
        } catch (Exception unavailable) { finish(attempt, "error", "provider", "", "", ""); }
    }

    private static void finish(int attempt, String state, String error, String uid, String name, String email) {
        Activity activity = owner.get(); if (activity == null) return;
        activity.runOnUiThread(() -> {
            if (attempt != generation) return;
            pending = null;
            if ("signedIn".equals(state)) preferences.edit().putBoolean("restore", true).apply();
            send(state, error, uid, name, email);
        });
    }

    public static void signOut() {
        Activity activity = owner.get(); if (activity == null) return;
        activity.runOnUiThread(() -> {
            final int attempt = ++generation;
            if (pending != null) pending.cancel(); pending = null;
            preferences.edit().putBoolean("restore", false).apply();
            send("guest", "", "", "", "");
            manager.clearCredentialStateAsync(new ClearCredentialStateRequest(), null, activity.getMainExecutor(),
                    new CredentialManagerCallback<Void, ClearCredentialException>() {
                @Override public void onResult(Void unused) { /* Already signed out locally. */ }
                @Override public void onError(ClearCredentialException error) {
                    if (attempt == generation) send("guest", "", "", "", "");
                }
            });
        });
    }

    public static void dispose() {
        Activity activity = owner.get(); if (activity == null) return;
        activity.runOnUiThread(() -> { generation++; if (pending != null) pending.cancel(); pending = null; callback = null; owner.clear(); });
    }

    private static void send(String state, String error, String uid, String name, String email) {
        if (callback == null) return;
        try {
            JSONObject result = new JSONObject(); result.put("state", state); result.put("error", error);
            result.put("uid", uid); result.put("name", name); result.put("email", email == null ? "" : email);
            callback.onResult(result.toString());
        } catch (Exception ignored) { /* Do not leak account data or token contents into logs. */ }
    }
}
