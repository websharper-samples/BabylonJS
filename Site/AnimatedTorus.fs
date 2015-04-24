namespace Site
 
open WebSharper
open WebSharper.JavaScript

[<JavaScript>]
module AnimatedTorus =
    open WebSharper.BabylonJs
    
    let Main container =
        let (engine, scene) = initializeSample container 640 360
        let light  = BABYLON.PointLight.Create(
                         "Sun",
                         BABYLON.Vector3.Create(0., 0., -15.),
                         scene
                     )
        let torus = BABYLON.Mesh.CreateTorus("Torus", 10., 1., 50., scene, true)
        let camera = BABYLON.ArcRotateCamera.Create(
                         "Camera",
                         0., 0., 0.,
                         BABYLON.Vector3.Zero(),
                         scene
                     )

        camera.setPosition(
            BABYLON.Vector3.Create(0., 0., -25.)
        )
        scene.activeCamera.attachControl (As canvas)

        engine.runRenderLoop (fun () ->
            scene.render()

            torus.rotation.x <- torus.rotation.x + (Math.PI / -180.)
        )

    let Sample =
        Samples.Build()
            .Id("AnimatedTorus")
            .FileName(__SOURCE_FILE__)
            .Keywords(["animation"; "torus"])
            .Render(Main)
            .Create()
