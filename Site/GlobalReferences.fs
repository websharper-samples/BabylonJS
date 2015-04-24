namespace Site

open WebSharper
open WebSharper.JavaScript
open WebSharper.BabylonJs

[<JavaScript; AutoOpen>]
module GlobalReferences =
    
    let canvas = JS.Document.CreateElement "canvas"

    let private engine : BABYLON.Engine.T option ref = ref None
    let scene : BABYLON.Scene.T option ref           = ref None

    let (!!) (a : 'a option ref) = (!a).Value

    let initializeSample (container : Dom.Element) (width : int) (height : int) =
        canvas.SetAttribute("width" , string width)
        canvas.SetAttribute("height", string height)

        container.AppendChild canvas |> ignore
        
        !engine
        |> Option.iter (fun engine ->
            engine.stopRenderLoop()
        )

        engine := Some
                  <| BABYLON.Engine.Create(
                         As canvas,
                         true
                  )
        
        !scene
        |> Option.iter (fun scene ->
            scene.activeCamera.detachControl (As canvas)
        )

        scene := Some
                 <| BABYLON.Scene.Create !!engine

        (!!engine, !!scene)

    let (!+) a = Samples.Set.Singleton a
