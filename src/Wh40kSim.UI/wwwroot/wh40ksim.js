// Raccourcis clavier des écrans de saisie (Quick sim, Full sim), à l'échelle du document : Ctrl+Entrée (lancer),
// Ctrl+S (enregistrer), Ctrl+O (ouvrir). L'action par défaut du navigateur (enregistrer la page…) est empêchée. Module
// chargé par ScenarioEditorPage, et par FullSimResultsPage pour sa barre d'onglets collante (watchSticky).

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

// Barre collante (écran des résultats du Full sim) : pose la classe « wh-stuck » sur `host` tant que `sentinel`, un
// repère vide placé juste au-dessus de la barre, est passé sous la barre de titre (hauteur `offset` en pixels), c'est-à-
// dire tant que la barre est collée en haut. Le CSS s'en sert pour afficher le bouton de retour compact.
export function watchSticky(sentinel, host, offset) {
    const observer = new IntersectionObserver(([entry]) => {
        host.classList.toggle("wh-stuck", !entry.isIntersecting && entry.boundingClientRect.top < offset);
    }, { rootMargin: `-${offset}px 0px 0px 0px`, threshold: 0 });
    observer.observe(sentinel);
    return { dispose: () => observer.disconnect() };
}

// Quick sim sur téléphone : les résultats arrivent sous la saisie ; à la fin d'un calcul, la page défile jusqu'à eux.
// Même seuil que la mise en page téléphone de wh40ksim.css. Sur un écran plus large, ils sont déjà visibles à droite.
export function scrollIntoViewOnPhone(element) {
    if (element && window.matchMedia("(max-width: 599.98px)").matches) {
        element.scrollIntoView({ behavior: "smooth", block: "start" });
    }
}
