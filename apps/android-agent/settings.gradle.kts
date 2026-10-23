pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
        // Mirrors are fallbacks only: a transient mirror 5xx must not block
        // the canonical repositories used by GitHub Actions.
        maven("https://maven.aliyun.com/repository/google")
        maven("https://maven.aliyun.com/repository/gradle-plugin")
        maven("https://maven.aliyun.com/repository/public")
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        // Binary Android plugins exported by private Unity project are local Maven dependencies,
        // NOT direct AAR files inside an Android library (AGP 8.x rejects those).
        val privateUnityRepo = System.getenv("QPS_UNITY_LIBRARY_DIR")
        if (!privateUnityRepo.isNullOrBlank()) {
            maven { url = uri(file("$privateUnityRepo/../private-maven")) }
        }
        maven("https://maven.aliyun.com/repository/google")
        maven("https://maven.aliyun.com/repository/public")
    }
}

rootProject.name = "QuestPhoneStreamAndroidAgent"
include(":app")

// Optional PRIVATE Unity runtime; never commit exported CAD/assets into the public repository.
// Requires QPS_UNITY_LIBRARY_DIR pointing to a local exported unityLibrary directory.
val privateUnity = System.getenv("QPS_UNITY_LIBRARY_DIR")
if (!privateUnity.isNullOrBlank()) {
    val dir = file(privateUnity)
    require(file("$privateUnity/build.gradle").isFile) { "QPS_UNITY_LIBRARY_DIR must point to unityLibrary" }
    include(":unityLibrary")
    project(":unityLibrary").projectDir = dir
}
