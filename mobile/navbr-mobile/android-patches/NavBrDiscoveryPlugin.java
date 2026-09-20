package br.navbr.mobile;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import org.json.JSONObject;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.SocketTimeoutException;
import java.nio.charset.StandardCharsets;

@CapacitorPlugin(name = "NavBrDiscovery")
public class NavBrDiscoveryPlugin extends Plugin {
    private static final int DISCOVERY_PORT = 27732;
    private static final String DISCOVERY_REQUEST = "NAVBR_DISCOVER_V1";

    @PluginMethod
    public void discover(PluginCall call) {
        Integer requestedTimeout = call.getInt("timeoutMs");
        final int timeoutMs = requestedTimeout == null
            ? 3500
            : Math.max(1000, Math.min(requestedTimeout, 10000));

        new Thread(() -> {
            try (DatagramSocket socket = new DatagramSocket()) {
                socket.setBroadcast(true);
                socket.setSoTimeout(timeoutMs);

                byte[] requestBytes = DISCOVERY_REQUEST.getBytes(StandardCharsets.US_ASCII);
                DatagramPacket request = new DatagramPacket(
                    requestBytes,
                    requestBytes.length,
                    InetAddress.getByName("255.255.255.255"),
                    DISCOVERY_PORT);
                socket.send(request);

                byte[] buffer = new byte[2048];
                DatagramPacket response = new DatagramPacket(buffer, buffer.length);
                socket.receive(response);

                String jsonText = new String(
                    response.getData(),
                    response.getOffset(),
                    response.getLength(),
                    StandardCharsets.UTF_8);
                JSONObject json = new JSONObject(jsonText);

                if (!"NavBR.MobileCompanion".equals(json.optString("service"))) {
                    call.reject("INVALID_NAVBR_DISCOVERY_RESPONSE");
                    return;
                }

                String pairingCode = json.optString("pairingCode", "");
                int httpPort = json.optInt("httpPort", 27731);
                if (pairingCode.isEmpty()) {
                    call.reject("NAVBR_DISCOVERY_MISSING_PAIRING");
                    return;
                }

                JSObject result = new JSObject();
                result.put("host", response.getAddress().getHostAddress());
                result.put("httpPort", httpPort);
                result.put("pairingCode", pairingCode);
                call.resolve(result);
            } catch (SocketTimeoutException timeout) {
                call.reject("NAVBR_NOT_FOUND");
            } catch (Exception ex) {
                call.reject("NAVBR_DISCOVERY_FAILED", ex);
            }
        }, "NavBR-Mobile-Discovery").start();
    }
}
