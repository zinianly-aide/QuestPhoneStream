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

    buildFeatures { buildConfig = true }

    defaultConfig {
        applicationId = "com.questphonestream.agent"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
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
