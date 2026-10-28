// Raccourcis clavier des écrans de saisie (Quick sim, Full sim), à l'échelle du document : Ctrl+Entrée (lancer),
// Ctrl+S (enregistrer), Ctrl+O (ouvrir). L'action par défaut du navigateur (enregistrer la page…) est empêchée. Module
// chargé par ScenarioEditorPage.

const shortcuts = { Enter: "enter", s: "s", S: "s", o: "o", O: "o" };

export function registerShortcuts(dotNet) {
    const handler = event => {
        if (!event.ctrlKey || event.altKey || event.shiftKey || event.metaKey) {
            return;
        }
        const key = shortcuts[event.key];
        if (key === undefined) {
            return;
        }
        event.preventDefault();
        // Un champ en cours de saisie ne transmet sa valeur qu'en le quittant : on le quitte d'abord, pour que le
        // raccourci agisse sur ce qui vient d'être tapé (l'événement « change » part avant l'appel ci-dessous)
        if (document.activeElement instanceof HTMLInputElement) {
            document.activeElement.blur();
        }
        dotNet.invokeMethodAsync("OnShortcut", key);
    };

    document.addEventListener("keydown", handler);
    return { dispose: () => document.removeEventListener("keydown", handler) };
}
