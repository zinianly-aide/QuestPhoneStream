# Phone media streaming MVP

This MVP adds a separate local-media path. The Android app owns the SAF-approved
`content://` URI and serves the original file through an embedded HTTP server;
the existing MediaProjection/WebRTC screen-sharing path is unchanged.

```text
Android SAF -> MediaShareRepository -> MediaHttpServer :8788
                                      ^ HTTP Range
Quest MediaCatalogClient -> VideoPlayer -> FlatMediaRenderer
```

## Android flow

1. Tap **Add Video** in the Android app.
2. Select a video with the system `ACTION_OPEN_DOCUMENT` picker. The app persists
   the read grant and stores metadata plus an opaque `media_...` id, never a real
   filesystem path or the `content://` URI in a network response.
3. Leave **Share** enabled. Disable it or remove the item to revoke access.
4. The app shows the LAN URL. Enter that URL manually in the Quest **Media HTTP URL** field;
   automatic NSD discovery is planned but is not enabled yet.

`MediaCatalog` is persisted in app preferences. `seekable` is detected from the
provider descriptor; a pipe-like provider is reported as non-seekable rather than
being advertised as a fully seekable file.

## HTTP API

| Request | Result |
| --- | --- |
| `GET /v1/media` | JSON array of shared metadata; requires pairing `Authorization: Bearer <token>` |
| `GET /v1/media/{id}` | One shared metadata object; requires pairing authorization |
| `POST /v1/media/{id}/play-token` | Short-lived capability for that item; requires pairing authorization |
| `HEAD /v1/media/{id}/content?cap=...` | Headers and length |
| `GET /v1/media/{id}/content?cap=...` | Original bytes, with Range support |

The catalog, metadata, and play-token control-plane endpoints require the same
pairing token used by signaling, sent as an `Authorization: Bearer` header.
Content access requires only the short-lived capability returned by `play-token`
(the Unity `VideoPlayer` URL cannot attach custom headers). The capability is scoped to
one media id, expires after five minutes, is cleared on app restart, and is
checked against the current Shared state on every request. Tokens are not put in
logs.

Supported ranges are single byte ranges such as `bytes=0-`, `bytes=1000-`, and
`bytes=1000-1999`. Successful ranges return `206`, `Accept-Ranges: bytes`,
`Content-Range`, `Content-Length`, and the source MIME type. Invalid ranges return
`416` with `Content-Range: bytes */TOTAL`. A request without Range returns `200`.
Multipart ranges are intentionally not supported in this MVP.

## Quest flow

Add `MediaCatalogClient`, `MediaPlaybackController`, and a separate
`FlatMediaRenderer` to the Quest scene. Enter the Android LAN URL in **Media HTTP
URL**, open **Video Library**, choose an item, request a play token, and pass the
resulting content URL to `MediaPlaybackController.PlayUrl`.

The playback controller supports Prepare/Play, Pause, Resume, Stop, time-based
seek, volume, and Ended/Error state callbacks. It uses Unity's built-in
`VideoPlayer` for ordinary 2D video only. It does not implement VR180, VR360,
stereo layouts, depth, transcoding, DRM, or automatic file copying.

Keep the media renderer and RenderTexture separate from the WebRTC receiver's
phone-screen texture. On a real scene, wire `MediaPlaybackController.renderer`
to a dedicated flat panel `Renderer`; do not reuse the WebRTC panel material.

## Local verification

From a machine on the same Wi-Fi, first list metadata:

```bash
curl -H 'Authorization: Bearer PAIRING_TOKEN' http://PHONE_IP:8788/v1/media
curl -H 'Authorization: Bearer PAIRING_TOKEN' -X POST http://PHONE_IP:8788/v1/media/MEDIA_ID/play-token
curl -H 'Range: bytes=0-1023' -H 'Accept: */*' \
  'http://PHONE_IP:8788/v1/media/MEDIA_ID/content?cap=CAPABILITY'
```

The Android and Quest must be able to reach each other directly; guest-network
client isolation or blocked multicast/routing will prevent this local path.

## Gate status

Automated JVM tests cover the catalog id/removal behavior and Range parser. Unity
and device gates remain **NOT RUN / NOT VERIFIED** until a Unity Editor, Android
device, and Quest 3S are available. Do not infer real playback compatibility from
source or unit tests.
