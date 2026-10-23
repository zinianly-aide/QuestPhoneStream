package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class ScenePreviewLanImportTest {
    @Test fun validPrivateIpv4Endpoints() {
        assertEquals(ScenePreviewLanImport.Endpoint("192.168.1.12", 51321),
            ScenePreviewLanImport.parseEndpoint("192.168.1.12:51321"))
        assertEquals(ScenePreviewLanImport.Endpoint("10.2.3.4", 12345),
            ScenePreviewLanImport.parseEndpoint("10.2.3.4:12345"))
        assertEquals(ScenePreviewLanImport.Endpoint("172.31.255.1", 65535),
            ScenePreviewLanImport.parseEndpoint("172.31.255.1:65535"))
    }

    @Test fun rejectPublicLoopbackHostnamesAndBadPorts() {
        for (bad in listOf(
            "127.0.0.1:12345", "8.8.8.8:12345", "example.com:4444", "169.254.1.10:2000",
            "172.32.0.2:12345", "192.168.1.12:0", "192.168.1.12:65536", "10.0.0.999:12345",
            "10.1.1.1", "10.1.1.1:-2", "2001:db8::1:12345", "192.168.1.12:443")) {
            assertThrows("unexpected accepted endpoint: $bad", IllegalArgumentException::class.java) {
                ScenePreviewLanImport.parseEndpoint(bad)
            }
        }
    }
}
