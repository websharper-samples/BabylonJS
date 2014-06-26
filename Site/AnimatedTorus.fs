namespace Site
 
open IntelliFactory.WebSharper

[<JavaScript>]
module AnimatedTorus =
    open IntelliFactory.WebSharper.BabylonJs
    open IntelliFactory.WebSharper.JQuery

    let Main (a : Dom.Element) =
        let (engine, scene) = InitializeSample a
        let light  = BABYLON.PointLight.Create(
                         "Lorem",
                         BABYLON.Vector3.Create(0., 0., -10.),
                         scene
                     )
        let torus = BABYLON.Mesh.CreateTorus("Ipsum", 10., 1., 50., scene, true)
        let camera = BABYLON.ArcRotateCamera.Create(
                         "Dolor",
                         0.,
                         0.,
                         0.,
                         BABYLON.Vector3.Zero(),
                         scene
                     )

        camera.setPosition(
            BABYLON.Vector3.Create(0., 5., -20.)
        )
        scene.activeCamera.attachControl (As canvas)

        engine.runRenderLoop (As (fun () ->
            scene.render() |> ignore

            torus.rotation.x <- torus.rotation.x + (EcmaScript.Math.PI / -180.)
        ))

    let Sample =
        Samples.Build()
            .Id("AnimatedTorus")
            .FileName(__SOURCE_FILE__)
            .Keywords(["animation"; "torus"])
            .Render(Main)
            .Create()
