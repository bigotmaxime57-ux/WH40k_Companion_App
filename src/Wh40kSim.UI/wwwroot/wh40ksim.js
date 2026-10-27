// Raccourcis clavier de la page « Simulateur », à l'échelle du document : Ctrl+Entrée (lancer), Ctrl+S (enregistrer),
// Ctrl+O (ouvrir). L'action par défaut du navigateur (enregistrer la page…) est empêchée. Module chargé par SimulatorPage.

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
        dotNet.invokeMethodAsync("OnShortcut", key);
    };

    document.addEventListener("keydown", handler);
    return { dispose: () => document.removeEventListener("keydown", handler) };
}
