package com.prdivyzavod.btlink;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothClass;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothServerSocket;
import android.bluetooth.BluetoothSocket;

import java.io.ByteArrayOutputStream;
import java.io.Closeable;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.UUID;

/**
 * A serial line between two paired phones over Bluetooth (RFCOMM), for the game's
 * two-player mode. One phone hosts and waits, the other joins it by its address.
 *
 * Everything that can block (waiting for the other phone, connecting, reading) runs
 * on a background thread; the game polls {@link #state()} and {@link #receive()} from
 * its own thread once a frame and never waits. All methods are static: there is one
 * line at a time. Nothing here throws - a failure ends up in {@link #problem()}.
 */
public final class BtLink {
    public static final int IDLE = 0, LISTENING = 1, CONNECTING = 2, CONNECTED = 3, FAILED = 4;

    private static final UUID SERVICE = UUID.fromString("5f0d7c1e-6b3a-4e52-9c47-a1d2e8f04780");
    private static final byte[] NOTHING = new byte[0];
    private static final Object LOCK = new Object();

    private static volatile int state = IDLE;
    private static volatile String problem = "";
    private static volatile String peer = "";
    private static int generation;                  // every host/join/close starts a new one; older threads see it and stop
    private static BluetoothServerSocket server;
    private static BluetoothSocket socket;
    private static OutputStream out;
    private static final ByteArrayOutputStream inbox = new ByteArrayOutputStream();

    private BtLink() { }

    private static BluetoothAdapter adapter() {
        return BluetoothAdapter.getDefaultAdapter();
    }

    /** True if this device has Bluetooth and it is switched on. */
    public static boolean ready() {
        try {
            BluetoothAdapter a = adapter();
            return a != null && a.isEnabled();
        } catch (Throwable t) {
            return false;
        }
    }

    /** The name other devices see this one under ("" if it cannot be read). */
    public static String localName() {
        try {
            String n = adapter().getName();
            return n != null ? n : "";
        } catch (Throwable t) {
            return "";
        }
    }

    /**
     * The phones, tablets and computers paired with this device, one "ADDRESS|NAME"
     * per line (headphones, watches, cars and the like are left out).
     */
    public static String paired() {
        StringBuilder sb = new StringBuilder();
        try {
            for (BluetoothDevice d : adapter().getBondedDevices()) {
                BluetoothClass c = d.getBluetoothClass();
                int major = c != null ? c.getMajorDeviceClass() : BluetoothClass.Device.Major.UNCATEGORIZED;
                if (major != BluetoothClass.Device.Major.PHONE && major != BluetoothClass.Device.Major.COMPUTER
                        && major != BluetoothClass.Device.Major.UNCATEGORIZED) {
                    continue;
                }
                String name = d.getName();
                sb.append(d.getAddress()).append('|').append(name != null ? name : d.getAddress()).append('\n');
            }
        } catch (Throwable t) {
            problem = describe(t);
        }
        return sb.toString();
    }

    /** Wait for a paired phone to join. */
    public static void host() {
        final int gen = restart(LISTENING);
        new Thread(() -> {
            try {
                BluetoothServerSocket s = adapter().listenUsingRfcommWithServiceRecord("PrdivyZavod", SERVICE);
                synchronized (LOCK) {
                    if (gen != generation) {
                        quietly(s);
                        return;
                    }
                    server = s;
                }
                BluetoothSocket other = s.accept();     // until the other phone connects, or close()
                quietly(s);
                serve(gen, other);
            } catch (Throwable t) {
                fail(gen, t);
            }
        }, "BtLink-host").start();
    }

    /** Join the game hosted by the paired phone with this address. */
    public static void join(final String address) {
        final int gen = restart(CONNECTING);
        new Thread(() -> {
            try {
                BluetoothAdapter a = adapter();
                try {
                    a.cancelDiscovery();                // a scan in progress would slow the connection down
                } catch (Throwable ignored) {
                    // (not allowed without the scan permission; nothing to cancel then)
                }
                BluetoothSocket s = a.getRemoteDevice(address).createRfcommSocketToServiceRecord(SERVICE);
                synchronized (LOCK) {
                    if (gen != generation) {
                        quietly(s);
                        return;
                    }
                    socket = s;
                }
                s.connect();
                serve(gen, s);
            } catch (Throwable t) {
                fail(gen, t);
            }
        }, "BtLink-join").start();
    }

    // The line is up: publish it, then keep reading until it breaks.
    private static void serve(int gen, BluetoothSocket s) throws IOException {
        InputStream in;
        synchronized (LOCK) {
            if (gen != generation) {
                quietly(s);
                return;
            }
            socket = s;
            server = null;
            out = s.getOutputStream();
            in = s.getInputStream();
            inbox.reset();
            try {
                String n = s.getRemoteDevice().getName();
                peer = n != null ? n : "";
            } catch (Throwable t) {
                peer = "";
            }
            state = CONNECTED;
        }
        byte[] buf = new byte[4096];
        while (true) {
            int n = in.read(buf);
            if (n < 0) {
                throw new IOException("the other phone hung up");
            }
            synchronized (LOCK) {
                if (gen != generation) {
                    return;
                }
                inbox.write(buf, 0, n);
            }
        }
    }

    public static int state() {
        return state;
    }

    /** Why the line failed ("" if it did not). */
    public static String problem() {
        return problem;
    }

    /** The other phone's name once connected. */
    public static String peer() {
        return peer;
    }

    /** Send the first {@code length} bytes of {@code data}. False if the line is down. */
    public static boolean send(byte[] data, int length) {
        OutputStream o;
        int gen;
        synchronized (LOCK) {
            o = out;
            gen = generation;
        }
        if (o == null) {
            return false;
        }
        try {
            o.write(data, 0, length);
            o.flush();
            return true;
        } catch (Throwable t) {
            fail(gen, t);
            return false;
        }
    }

    /** Everything that has arrived since the last call (empty if nothing has). */
    public static byte[] receive() {
        synchronized (LOCK) {
            if (inbox.size() == 0) {
                return NOTHING;
            }
            byte[] b = inbox.toByteArray();
            inbox.reset();
            return b;
        }
    }

    /** Hang up, or stop waiting. */
    public static void close() {
        synchronized (LOCK) {
            generation++;
            state = IDLE;
            shut();
        }
    }

    private static int restart(int newState) {
        synchronized (LOCK) {
            generation++;
            shut();
            problem = "";
            peer = "";
            state = newState;
            return generation;
        }
    }

    private static void fail(int gen, Throwable t) {
        synchronized (LOCK) {
            if (gen != generation) {
                return;         // closed on purpose meanwhile
            }
            problem = describe(t);
            state = FAILED;
            shut();
        }
    }

    private static void shut() {
        quietly(server);
        quietly(socket);
        server = null;
        socket = null;
        out = null;
    }

    private static void quietly(Closeable c) {
        if (c == null) {
            return;
        }
        try {
            c.close();
        } catch (Throwable ignored) {
            // closing is best effort
        }
    }

    private static String describe(Throwable t) {
        String m = t.getMessage();
        return t.getClass().getSimpleName() + (m != null ? ": " + m : "");
    }
}
