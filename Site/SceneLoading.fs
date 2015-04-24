namespace Site
 
open WebSharper

[<JavaScript>]
module SceneLoading =
    open WebSharper.JavaScript
    open WebSharper.BabylonJs

    let Main container =
        let (engine, _) = initializeSample container 640 360
        
        BABYLON.SceneLoader.Load("Resources/Scenes/Spaceship/", "Spaceship.babylon", engine, (fun (scene : BABYLON.Scene.T) ->
            scene.executeWhenReady (fun () ->
                scene.activeCamera.attachControl (As canvas)

                engine.runRenderLoop (fun () ->
                    scene.render()
                )

                GlobalReferences.scene := Some scene
            )
        ), ignore)
        
    let Sample =
        Samples.Build()
            .Id("SceneLoading")
            .FileName(__SOURCE_FILE__)
            .Keywords(["scene"; "external"; "model"])
            .Render(Main)
            .Create()