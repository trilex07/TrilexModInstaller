# Darkness II language selection repair (2026-11-08)

Branch: codex/darkness-language-20261108, based on cf21442.

The shipped core INI contains PolishText=1 and PolishVoice=1 even when the
installer choices are English. The runtime VR panel was also hardcoded Polish.

DarknessLanguageSettings now applies the user's text choice and English voice
(the reviewed public catalog has no Polish dubbing module) after the source
package is verified and before prepared output hashes are recorded. It records
matching Applied* flags. Signed archives are not edited. Manual exports use the
same derivation, with the source checksum checked before the scoped transform.
English plans select the existing English native-menu resources; Polish plans
select polish-text instead. Non-Darkness games and other targets are unchanged.

Validation: dotnet run --project tests/Trilex.Installer.Tests -c Release
--no-restore: 100 passed, 0 failed. New tests cover install/export/uninstall,
English legacy-INI migration, PL text/EN voice, tampered prepared inputs,
matching resource selection and other-game exclusion. Source-only changes:
no application upgrade, package signing, release or external push performed.

Companion runtime: Darkness2VR branch codex/ladder-language-20261108 localizes
VR settings from PolishText and defaults absent flags to English. Its selected
Polish flag applies to game text and the VR panel; game archives change after
exit through the existing helper, panel language changes immediately.

Packaging gap to preserve in follow-up: the public vr-core archive currently
contains four files, not the private VRSettings language helper/profiles.
The local Steam installation has verified private EN/PL profiles and supports
in-menu switching, but do not advertise the public package's language switching
as complete until helper/profiles are included as local reconstructions and
end-to-end tested. No Polish audio is authorized for public redistribution by
this change. This repair does not diagnose or fix ladder dismounting.
