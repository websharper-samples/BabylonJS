namespace Site

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.JQuery
open IntelliFactory.WebSharper.BabylonJs

[<JavaScript; AutoOpen>]
module GlobalReferences =
    let canvas =
        (JQuery.Of "<canvas height=\"360\" width=\"640\"></canvas>").Get 0 :?> Dom.Element

    let engine : BABYLON.Engine.T option ref = ref None

    let resetEngine () =
        match !engine with
        | Some engine ->
            engine.stopRenderLoop()
        | _ -> 
            ()

        engine := Some (BABYLON.Engine.Create(
                           As canvas,
                           true
                       ))

    let scene : BABYLON.Scene.T option ref = ref None

    let resetScene () =
        match !scene with
        | Some scene ->
            scene.activeCamera.detachControl (As canvas)
        | _ -> 
            ()

        scene := Some (BABYLON.Scene.Create (!engine).Value)

    let InitializeSample (container : Dom.Element) =
        (JQuery.Of container).Append(canvas).Ignore
        
        resetEngine()
        resetScene()

        ((!engine).Value, (!scene).Value)
