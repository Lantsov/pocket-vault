"""Verify release ZIP integrity, contents, attribution bytes and SHA-256; extract for smoke tests."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
version = ET.parse(ROOT / 'PocketVault.App/PocketVault.App.csproj').findtext('.//Version')
output = ROOT / 'dist/releases'
assets = json.loads((ROOT / 'PocketVault.App/obj/project.assets.json').read_text(encoding='utf-8'))
sdk_root = Path(next(iter(assets['project']['frameworks'].values()))['runtimeIdentifierGraphPath']).parents[2]
packs = {}
for framework in assets['project']['frameworks'].values():
    for dependency in framework.get('downloadDependencies', []):
        name = dependency['name']
        if name not in ('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64'):
            continue
        match = re.fullmatch(r'\[([^,]+), \1\]', dependency['version'])
        assert match, f'Non-exact runtime version: {dependency}'
        pack_version = match[1]
        pack_root = next(Path(folder) / name.lower() / pack_version for folder in assets['packageFolders']
                         if (Path(folder) / name.lower() / pack_version).is_dir())
        packs[name] = (pack_version, pack_root)
assert len(packs) == 2, 'Publish self-contained before verification'
sums = {}
for line in (output / 'SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
    digest, name = line.split('  ')
    assert re.fullmatch('[0-9a-f]{64}', digest) and name not in sums
    sums[name] = digest
expected_names = {f'PocketVault-{version}-win-x64-{variant}.zip'
                  for variant in ('self-contained', 'framework-dependent')}
assert set(sums) == expected_names
for variant in ('self-contained', 'framework-dependent'):
    name = f'PocketVault-{version}-win-x64-{variant}.zip'
    archive_path = output / name
    assert hashlib.sha256(archive_path.read_bytes()).hexdigest() == sums[name], name
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None, f'CRC failure: {name}'
        entries = archive.namelist()
        assert len(entries) == len(set(entries)), 'Duplicate ZIP entries'
        expected = {'LICENSE': (ROOT / 'LICENSE').read_bytes(),
                    'THIRD_PARTY_NOTICES.md': (ROOT / 'docs/THIRD_PARTY_NOTICES.md').read_bytes()}
        selected = packs if variant == 'self-contained' else {'Microsoft.NETCore.App.Runtime.win-x64': packs['Microsoft.NETCore.App.Runtime.win-x64']}
        for pack, (pack_version, pack_root) in selected.items():
            for source in pack_root.iterdir():
                if source.is_file() and re.fullmatch(r'(LICENSE|THIRD[-_]?PARTY[-_]?NOTICES)(\.TXT)?', source.name, re.I):
                    expected[f'runtime-notices/{pack}-{pack_version}/{source.name}'] = source.read_bytes()
        for entry, data in expected.items():
            assert archive.read(entry) == data, f'Incorrect upstream notice: {entry}'
        core_version = packs['Microsoft.NETCore.App.Runtime.win-x64'][0]
        windows_prefix = f'runtime-notices/Microsoft.NET-Windows-{core_version}/'
        for document in ('LICENSE.txt', 'ThirdPartyNotices.txt'):
            assert archive.read(windows_prefix + document) == (sdk_root / document).read_bytes()
        assert b'MICROSOFT .NET LIBRARY' in archive.read(windows_prefix + 'LICENSE.txt')
        assert len(archive.read(windows_prefix + 'ThirdPartyNotices.txt')) > 1000
        allowed = set(expected) | {windows_prefix + 'LICENSE.txt', windows_prefix + 'ThirdPartyNotices.txt'}
        if variant == 'self-contained':
            entry = windows_prefix + 'WINDOWS-SDK-LICENSE.html'
            assert archive.read(entry) == (ROOT / 'docs/licenses/WINDOWS-SDK-LICENSE.html').read_bytes()
            allowed.add(entry)
        published = ROOT / 'dist' / variant
        binaries = {'PocketVault.exe'} if variant == 'self-contained' else {
            'PocketVault.exe', 'PocketVault.dll', 'PocketVault.Core.dll',
            'PocketVault.deps.json', 'PocketVault.runtimeconfig.json'}
        for binary in binaries:
            assert archive.read(binary) == (published / binary).read_bytes(), f'Stale published binary: {binary}'
        assert set(entries) == allowed | binaries, f'Unexpected/missing entries: {set(entries) ^ (allowed | binaries)}'
        if variant == 'framework-dependent':
            config = json.loads(archive.read('PocketVault.runtimeconfig.json'))['runtimeOptions']
            assert 'includedFrameworks' not in config
            assert {f['name'] for f in config['frameworks']} == {'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App'}
            assert all(f['version'].startswith('10.0.') for f in config['frameworks'])
        destination = ROOT / 'dist/verified-release' / variant
        assert not destination.exists(), f'Refusing to mix extracted output: {destination}'
        archive.extractall(destination)
        print(f'[OK] {name}: CRC, SHA-256, notices and exact file manifest ({len(entries)} files)')
