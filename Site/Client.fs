namespace Site

open IntelliFactory.WebSharper

[<JavaScript>]
module Client =
    let All =
        Samples.Set.Create [
            !+ Cube.Sample
            !+ AnimatedTorus.Sample
            !+ SceneLoading.Sample
        ]

    let Main = All.Show()
