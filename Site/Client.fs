namespace Site

open WebSharper

[<JavaScript>]
module Client =
    let All =
        Samples.Set.Create [
            !+ Cube.Sample
            !+ AnimatedTorus.Sample
            !+ SceneLoading.Sample
        ]

    [<SPAEntryPoint>]
    let Main() =
        All.Show()
