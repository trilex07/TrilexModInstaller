"""Check representative native game labels before building a language pack."""

MENU_MARKER = b"/D2/Language/Menu/"
LABELS = {
    "en": ("Continue", "Options", "<MENU_CANCEL>Back"),
    "pl": ("Kontynuuj", "Opcje", "<MENU_CANCEL>Wstecz"),
}


def validate_menu_language(tool, payload, language):
    labels = dict((key, text) for key, text, _ in tool.category(payload, MENU_MARKER)[3])
    keys = ("MainMenu_Item_Continue", "MainMenu_Item_Options", "Shared_Back")
    expected = LABELS[language]
    for key, label in zip(keys, expected):
        if labels.get(key) != label:
            raise ValueError(f"Wrong native {language.upper()} menu label for {key}: "
                             f"expected {label!r}, got {labels.get(key)!r}")
