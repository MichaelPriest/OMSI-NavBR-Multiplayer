package br.navbr.mobile;

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        registerPlugin(NavBrDiscoveryPlugin.class);
        super.onCreate(savedInstanceState);
    }
}
