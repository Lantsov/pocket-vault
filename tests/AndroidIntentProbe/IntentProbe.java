package com.lantsov.pocketvault.intentprobe;

import android.app.Activity;
import android.content.*;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.*;
import android.provider.OpenableColumns;
import java.io.*;

// Test-only sender/provider. Opaque IDs deliberately contain no filename.
public final class IntentProbe extends Activity {
    static final String AUTHORITY = "com.lantsov.pocketvault.intentprobe.documents";

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (getIntent().getBooleanExtra("revoke", false)) {
            for (String id : new String[] {"17", "18", "19"})
                revokeUriPermission(Uri.parse("content://" + AUTHORITY + "/" + id),
                    Intent.FLAG_GRANT_READ_URI_PERMISSION);
        } else {
            String id = getIntent().getStringExtra("id");
            if (id == null) id = "17";
            Intent view = new Intent(Intent.ACTION_VIEW);
            view.setDataAndType(Uri.parse("content://" + AUTHORITY + "/" + id),
                "application/vnd.pocketvault");
            view.setPackage("com.lantsov.pocketvault");
            view.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION |
                Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(view);
        }
        finish();
    }

    public static final class Provider extends ContentProvider {
        private File fixture;
        @Override public boolean onCreate() {
            fixture = new File(getContext().getFilesDir(), "public-fixture.pvlt");
            try (InputStream in = getContext().getAssets().open("fixture.pvlt");
                 OutputStream out = new FileOutputStream(fixture)) {
                byte[] block = new byte[4096]; int count;
                while ((count = in.read(block)) != -1) out.write(block, 0, count);
            } catch (IOException e) { throw new IllegalStateException("Test fixture unavailable"); }
            return true;
        }
        @Override public String getType(Uri uri) { return "application/vnd.pocketvault"; }
        @Override public Cursor query(Uri uri, String[] projection, String selection,
                String[] selectionArgs, String sortOrder) {
            String id = uri.getLastPathSegment();
            String name = "19".equals(id) ? "wrong.bin" : "18".equals(id) ? "warm.pvlt" : "cold.PVLT";
            String[] columns = projection == null ?
                new String[] {OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE} : projection;
            MatrixCursor cursor = new MatrixCursor(columns);
            Object[] row = new Object[columns.length];
            for (int i = 0; i < columns.length; i++) {
                if (OpenableColumns.DISPLAY_NAME.equals(columns[i])) row[i] = name;
                if (OpenableColumns.SIZE.equals(columns[i])) row[i] = fixture.length();
            }
            cursor.addRow(row); return cursor;
        }
        @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
            if (!"r".equals(mode)) throw new FileNotFoundException("Read only test fixture");
            return ParcelFileDescriptor.open(fixture, ParcelFileDescriptor.MODE_READ_ONLY);
        }
        @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException(); }
        @Override public int update(Uri uri, ContentValues values, String s, String[] a) { throw new UnsupportedOperationException(); }
        @Override public int delete(Uri uri, String s, String[] a) { throw new UnsupportedOperationException(); }
    }
}
