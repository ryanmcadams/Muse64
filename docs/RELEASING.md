# Releasing

Releases are built by GitHub Actions ([`.github/workflows/release.yml`](../.github/workflows/release.yml)) when a `v*` tag is pushed.

## Cut a release

1. Bump the version in `src/MuseApp/MuseApp.csproj`, all three together:

   ```xml
   <AssemblyVersion>2.1.0.0</AssemblyVersion>
   <FileVersion>2.1.0.0</FileVersion>
   <Version>2.1.0</Version>
   ```

2. Check it locally:

   ```powershell
   ./scripts/publish.ps1
   ```

   Then smoke-test the exe: it starts and shows the sign-in page, the tray icon and menu work (Quit really exits), `MuseApp.exe --minimized` starts with no window on screen, a second launch brings the window forward, and `Ctrl+Alt+M` shows/hides it. Also bump `assemblyIdentity version` in `src/MuseApp/app.manifest`.

3. Merge to `master` and wait for CI to go green.

4. Tag and push the tag. The tag must be exactly `v` + `<Version>`:

   ```powershell
   git tag v2.1.0
   git push origin v2.1.0
   ```

## What the workflow does

1. Checks that the tag equals `v$(Version)` from the csproj, and fails with a clear error if not.
2. Restores, builds (warnings are errors) and runs the tests on `windows-latest`.
3. Publishes the single-file, self-contained, compressed `MuseApp.exe` (about 70 MB for 2.1.0).
4. Zips it as `MuseForWindows-vX.Y.Z-win-x64.zip` and writes `SHA256SUMS.txt`.
5. Creates a GitHub Release for the tag with both files attached and auto-generated release notes.

If the tag was wrong, delete it (`git push --delete origin vX.Y.Z`, then `git tag -d vX.Y.Z`), fix, and re-tag.

Users can verify a download with:

```powershell
(Get-FileHash .\MuseForWindows-v2.1.0-win-x64.zip -Algorithm SHA256).Hash
```

## SmartScreen and code signing

`MuseApp.exe` is **not code-signed**. The first time someone runs a new download, Windows SmartScreen shows *"Windows protected your PC"* with **Unknown publisher**. To run it anyway: click **More info → Run anyway**. The warning fades as a file builds reputation, but starts over with every new release.

The real fix is signing the exe, with either:

- **Azure Trusted Signing** (cheapest; `azure/trusted-signing-action`), or
- an **OV/EV code-signing certificate** via `signtool`.

The signing step goes in `release.yml` after **Publish** and before **Package**, so the zip and hash cover the signed exe. A commented-out placeholder is already there:

```yaml
# - name: Sign
#   run: |
#     signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
#       /f cert.pfx /p "${{ secrets.SIGNING_CERT_PASSWORD }}" publish/MuseApp.exe
```

Keep the certificate and password in repository secrets. Never commit them.

## winget

Once releases are stable (and ideally signed), a manifest can be submitted to [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) pointing at the release zip and its SHA256 (for example with `wingetcreate new <zip-url>`). Not done yet.
