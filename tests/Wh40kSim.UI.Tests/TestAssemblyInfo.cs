// Les composants MudBlazor finissent certains rendus en arrière-plan (infobulles, popovers) : exécutées en parallèle,
// les classes de tests chargent la machine et une interaction bUnit (clic, saisie) se perdait parfois. Les tests de
// l'interface s'exécutent donc les uns après les autres.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
