# Installation activity

The desktop now displays one localized `Installing the mod…` / `Instalowanie
moda…` message across downloads, extraction, local asset generation, backup,
application and verification. The busy progress bar is indeterminate: preparation
has no defensible overall percentage or time estimate. Success, error and
cancellation replace this message explicitly.

Technical phase/item changes go to the existing local installer log. Extraction
and preparation report before expensive work as well as after it. Late progress
callbacks cannot overwrite the final result. Same-value language binding no
longer resets the status to initial folder selection, and language/back controls
are disabled while busy.

Verification: 64 core regressions passed, including actual transaction phase
ordering and cancellation before game writes. WPF tests cover installation
status, busy navigation/cancel/indeterminate bindings, language rebinding, late
progress after an error and cancellation in both languages and eight layouts.
The UI test uses a synthetic pending operation, never a game installation.

The user's RC3 installation completed and verified V7 at 22:58 on 2026-10-26.
That confirms the installer transaction, not the completeness or physical
behavior of every feature in its manifest. Subsequent missing wheel/movie
dependencies are tracked separately and must be added to the package.
