package com.questphonestream.camera;

import android.content.Context;
import android.graphics.ImageFormat;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CaptureRequest;
import android.hardware.camera2.params.StreamConfigurationMap;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.util.Size;
import android.view.Surface;

import java.nio.ByteBuffer;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.Semaphore;
import java.util.concurrent.TimeUnit;

/**
 * Opens the Quest headset camera ("50" on Quest 3S) through the standard Android Camera2
 * API and exposes the latest frame as an RGBA byte[] to Unity.
 *
 * Permission: horizonos.permission.HEADSET_CAMERA must be granted to the app.
 * The camera is opened with YUV_420_888 and converted to RGBA on the callback thread.
 */
public class QuestHeadsetCamera {
    private static final String TAG = "QuestHeadsetCamera";
    private static final long OPEN_TIMEOUT_MS = 4000;

    private static QuestHeadsetCamera sInstance;

    private final Context mContext;
    private final CameraManager mManager;
    private final Semaphore mOpenLock = new Semaphore(1);

    private HandlerThread mThread;
    private Handler mHandler;
    private CameraDevice mDevice;
    private CameraCaptureSession mSession;
    private ImageReader mReader;
    private CaptureRequest.Builder mPreviewBuilder;

    private int mWidth;
    private int mHeight;
    private volatile byte[] mLatestFrame;
    private volatile boolean mRunning;
    private volatile String mLastError;

    public static synchronized QuestHeadsetCamera getInstance(Context context) {
        if (sInstance == null) {
            sInstance = new QuestHeadsetCamera(context.getApplicationContext());
        }
        return sInstance;
    }

    private QuestHeadsetCamera(Context context) {
        mContext = context;
        mManager = (CameraManager) context.getSystemService(Context.CAMERA_SERVICE);
    }

    /**
     * Opens the camera and starts a repeating preview. Blocks until the session is configured.
     * @return true on success; call lastError() on failure.
     */
    public boolean open(String cameraId, int width, int height) {
        close();
        mLastError = null;
        try {
            if (mManager == null) {
                mLastError = "no CameraManager";
                return false;
            }

            mThread = new HandlerThread("QuestHeadsetCamera");
            mThread.start();
            mHandler = new Handler(mThread.getLooper());

            CameraCharacteristics chars = mManager.getCameraCharacteristics(cameraId);
            StreamConfigurationMap map = chars.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP);
            if (map == null) {
                mLastError = "no stream map";
                return false;
            }

            Size chosen = pickSize(map, width, height);
            if (chosen == null) {
                mLastError = "no matching size";
                return false;
            }
            mWidth = chosen.getWidth();
            mHeight = chosen.getHeight();

            mReader = ImageReader.newInstance(mWidth, mHeight, ImageFormat.YUV_420_888, 2);
            mReader.setOnImageAvailableListener(this::onImageAvailable, mHandler);

            if (!mOpenLock.tryAcquire(2500, TimeUnit.MILLISECONDS)) {
                mLastError = "open lock busy";
                return false;
            }
            mManager.openCamera(cameraId, new CameraDevice.StateCallback() {
                @Override
                public void onOpened(CameraDevice camera) {
                    mDevice = camera;
                    mOpenLock.release();
                    configureSession(camera);
                }

                @Override
                public void onDisconnected(CameraDevice camera) {
                    Log.w(TAG, "camera disconnected");
                    if (mDevice == camera) mDevice = null;
                    camera.close();
                    mOpenLock.release();
                }

                @Override
                public void onError(CameraDevice camera, int error) {
                    Log.e(TAG, "open error code=" + error);
                    mLastError = "open error " + error;
                    if (mDevice == camera) mDevice = null;
                    camera.close();
                    mOpenLock.release();
                }
            }, mHandler);

            if (!mOpenLock.tryAcquire(OPEN_TIMEOUT_MS, TimeUnit.MILLISECONDS)) {
                mLastError = "open timeout";
                return false;
            }
            mOpenLock.release();

            // Wait for the capture session to start streaming.
            long deadline = System.currentTimeMillis() + 3000;
            while (!mRunning && System.currentTimeMillis() < deadline) {
                Thread.sleep(20);
            }
            return mDevice != null && mRunning;
        } catch (Exception e) {
            mLastError = e.toString();
            Log.e(TAG, "open failed", e);
            return false;
        }
    }

    private static Size pickSize(StreamConfigurationMap map, int width, int height) {
        Size[] sizes = map.getOutputSizes(ImageFormat.YUV_420_888);
        if (sizes == null || sizes.length == 0) return null;
        Size best = null;
        for (Size s : sizes) {
            if (s.getWidth() <= width && s.getHeight() <= height) {
                if (best == null
                        || (s.getWidth() * s.getHeight() > best.getWidth() * best.getHeight())) {
                    best = s;
                }
            }
        }
        if (best == null) {
            for (Size s : sizes) {
                if (best == null || s.getWidth() * s.getHeight() > best.getWidth() * best.getHeight()) {
                    best = s;
                }
            }
        }
        return best;
    }

    private void configureSession(CameraDevice camera) {
        try {
            List<Surface> outputs = Arrays.asList(mReader.getSurface());
            mPreviewBuilder = camera.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
            mPreviewBuilder.addTarget(mReader.getSurface());
            camera.createCaptureSession(outputs, new CameraCaptureSession.StateCallback() {
                @Override
                public void onConfigured(CameraCaptureSession session) {
                    mSession = session;
                    try {
                        session.setRepeatingRequest(mPreviewBuilder.build(), null, mHandler);
                        mRunning = true;
                        Log.i(TAG, "streaming " + mWidth + "x" + mHeight);
                    } catch (Exception e) {
                        mLastError = e.toString();
                        Log.e(TAG, "repeating request failed", e);
                    }
                }

                @Override
                public void onConfigureFailed(CameraCaptureSession session) {
                    mLastError = "session configure failed";
                    Log.e(TAG, "session configure failed");
                }
            }, mHandler);
        } catch (Exception e) {
            mLastError = e.toString();
            Log.e(TAG, "configure failed", e);
        }
    }

    private void onImageAvailable(ImageReader reader) {
        Image image = null;
        try {
            image = reader.acquireLatestImage();
            if (image == null) return;
            Image.Plane[] planes = image.getPlanes();
            ByteBuffer y = planes[0].getBuffer();
            ByteBuffer u = planes[1].getBuffer();
            ByteBuffer v = planes[2].getBuffer();
            int yRowStride = planes[0].getRowStride();
            int uvRowStride = planes[1].getRowStride();
            int uvPixelStride = planes[1].getPixelStride();
            int w = mWidth;
            int h = mHeight;
            byte[] rgba = new byte[w * h * 4];
            yuvToRgba(y, u, v, rgba, w, h, yRowStride, uvRowStride, uvPixelStride);
            mLatestFrame = rgba;
        } catch (Exception e) {
            Log.e(TAG, "frame error: " + e);
        } finally {
            if (image != null) image.close();
        }
    }

    private static void yuvToRgba(ByteBuffer y, ByteBuffer u, ByteBuffer v, byte[] rgba, int w, int h,
                                  int yRowStride, int uvRowStride, int uvPixelStride) {
        int outStride = w * 4;
        for (int row = 0; row < h; row++) {
            int yBase = row * yRowStride;
            int uvRow = row >> 1;
            int uvBase = uvRow * uvRowStride;
            int outBase = row * outStride;
            for (int col = 0; col < w; col++) {
                int Y = (y.get(yBase + col) & 0xFF) - 16;
                int uvCol = (col >> 1) * uvPixelStride;
                int U = (u.get(uvBase + uvCol) & 0xFF) - 128;
                int V = (v.get(uvBase + uvCol) & 0xFF) - 128;
                if (Y < 0) Y = 0;
                int r = (int) (1.164f * Y + 1.596f * V);
                int g = (int) (1.164f * Y - 0.813f * V - 0.391f * U);
                int b = (int) (1.164f * Y + 2.018f * U);
                int out = outBase + col * 4;
                rgba[out] = (byte) clamp(r);
                rgba[out + 1] = (byte) clamp(g);
                rgba[out + 2] = (byte) clamp(b);
                rgba[out + 3] = (byte) 255;
            }
        }
    }

    private static int clamp(int x) {
        return x < 0 ? 0 : (x > 255 ? 255 : x);
    }

    public byte[] capture() {
        return mLatestFrame;
    }

    public int width() {
        return mWidth;
    }

    public int height() {
        return mHeight;
    }

    public boolean isRunning() {
        return mRunning;
    }

    public String lastError() {
        return mLastError;
    }

    public void close() {
        mRunning = false;
        if (mSession != null) {
            try { mSession.close(); } catch (Exception ignored) { }
            mSession = null;
        }
        if (mDevice != null) {
            mDevice.close();
            mDevice = null;
        }
        if (mReader != null) {
            mReader.close();
            mReader = null;
        }
        if (mThread != null) {
            mThread.quitSafely();
            mThread = null;
        }
        mLatestFrame = null;
    }
}
