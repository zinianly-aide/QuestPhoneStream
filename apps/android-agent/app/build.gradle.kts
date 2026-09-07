plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.questphonestream.agent"
    compileSdk = 36
    buildToolsVersion = "36.1.0"

    defaultConfig {
        applicationId = "com.questphonestream.agent"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    // Release signing: inject keystore via CI env vars (KEYSTORE_BASE64 etc).
    // Locally without these vars, release falls back to unsigned.
    signingConfigs {
        create("release") {
            val b64 = System.getenv("KEYSTORE_BASE64")
            if (!b64.isNullOrBlank()) {
                val ksFile = file("${rootDir}/release.keystore")
                if (!ksFile.exists()) {
                    ksFile.writeBytes(java.util.Base64.getDecoder().decode(b64))
                }
                storeFile = ksFile
                storePassword = System.getenv("KEYSTORE_PASSWORD") ?: ""
                keyAlias = System.getenv("KEY_ALIAS") ?: ""
                keyPassword = System.getenv("KEY_PASSWORD") ?: ""
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = if (System.getenv("KEYSTORE_BASE64").isNullOrBlank())
                null
            else
                signingConfigs.getByName("release")
        }
    }
}

dependencies {
    implementation("androidx.activity:activity-ktx:1.9.3")
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.webrtc:google-webrtc:1.0.32006")
    testImplementation("junit:junit:4.13.2")
}
