namespace Site
 
open IntelliFactory.WebSharper

[<JavaScript>]
module Cube =
    open IntelliFactory.WebSharper.BabylonJs
    open IntelliFactory.WebSharper.JQuery

    let Main a =
        let (engine, scene) = InitializeSample a
        let light  = BABYLON.PointLight.Create(
                         "Lorem",
                         BABYLON.Vector3.Create(10., 10., -10.),
                         scene
                     )
        let cube   = BABYLON.Mesh.CreateBox("Ipsum", 10., scene)
        let camera = BABYLON.FreeCamera.Create(
                         "Dolor",
                         BABYLON.Vector3.Create(0., 0., -25.),
                         scene
                     )

        scene.activeCamera.attachControl (As canvas)

        engine.runRenderLoop (As (fun () ->
            scene.render()
        ))

    let Sample =
        Samples.Build()
            .Id("Cube")
            .FileName(__SOURCE_FILE__)
            .Keywords(["basics"; "cube"])
            .Render(Main)
            .Create()
