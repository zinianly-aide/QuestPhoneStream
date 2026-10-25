plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.questphonestream.agent"
    compileSdk = 36
    // Match the Unity 2022 IL2CPP toolchain in optional private unityLibrary.
    ndkVersion = "23.1.7779620"
    buildToolsVersion = "36.1.0"

    // Optional private Unity is isolated in a separate Android process to keep
    // WebRTC signaling/capture/UI alive if Unity's native renderer crashes.
    // The native-only CI APK does not receive the Unity activity overlay.
    if (System.getenv("QPS_UNITY_LIBRARY_DIR") != null) {
        sourceSets.getByName("debug").manifest.srcFile("src/unityOverlay/AndroidManifest.xml")
        sourceSets.getByName("release").manifest.srcFile("src/unityOverlay/AndroidManifest.xml")
        // Unity 2022 places game_view_content_description in its standalone
        // launcher/src/main/res/values/strings.xml, not in exported unityLibrary.
        // The integrated :app is now the launcher: include only the required
        // host string, without importing Unity's private app label or icons.
        sourceSets.getByName("debug").res.srcDir("src/unityOverlay/res")
        sourceSets.getByName("release").res.srcDir("src/unityOverlay/res")
    }

    buildFeatures { buildConfig = true }

    defaultConfig {
        applicationId = "com.questphonestream.agent"
        minSdk = 29
        targetSdk = 36
        // Bundled Unity furniture demo is an opt-in developer build; native-only
        // CI retains its original package version for stable downstream users.
        versionCode = if (System.getenv("QPS_UNITY_LIBRARY_DIR") != null) 18 else 1
        versionName = if (System.getenv("QPS_UNITY_LIBRARY_DIR") != null) "3.4.1-m4" else "0.1.0"
        buildConfigField("boolean", "HAS_UNITY_HOME", (System.getenv("QPS_UNITY_LIBRARY_DIR") != null).toString())
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    if (System.getenv("QPS_UNITY_LIBRARY_DIR") != null) implementation(project(":unityLibrary"))
    implementation("androidx.activity:activity-ktx:1.9.3")
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.webrtc:google-webrtc:1.0.32006")
    testImplementation("junit:junit:4.13.2")
}
