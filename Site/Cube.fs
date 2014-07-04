namespace Site
 
open IntelliFactory.WebSharper

[<JavaScript>]
module Cube =
    open IntelliFactory.WebSharper.BabylonJs

    let Main container =
        let (engine, scene) = initializeSample container 640 360
        let light  = BABYLON.PointLight.Create(
                         "Sun",
                         BABYLON.Vector3.Create(0., 10., -15.),
                         scene
                     )
        let cube   = BABYLON.Mesh.CreateBox("Cube", 10., scene)
        let camera = BABYLON.FreeCamera.Create(
                         "Camera",
                         BABYLON.Vector3.Create(0., 0., -25.),
                         scene
                     )

        scene.activeCamera.attachControl (As canvas)

        engine.runRenderLoop (fun () ->
            scene.render()
        )

    let Sample =
        Samples.Build()
            .Id("Cube")
            .FileName(__SOURCE_FILE__)
            .Keywords(["basics"; "cube"])
            .Render(Main)
            .Create()
