namespace BabylonJS

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.BabylonJs

[<JavaScript>]
module Client =

    // The template is the whole document, loaded from the DOM, so editing
    // index.html and refreshing the browser is enough.
    type IndexTemplate = Template<"index.html", ClientLoad.FromDocument>

    // ------------------------------------------------------------------
    // State bound into the template
    // ------------------------------------------------------------------

    let status = Var.Create "Building the scene…"
    let notice = Var.Create "Drag to orbit, scroll to zoom, click a mesh to pick it."
    let engineInfo = Var.Create "-"
    let fps = Var.Create "-"
    let cameraInfo = Var.Create "-"
    let lightInfo = Var.Create "-"
    let materialInfo = Var.Create "-"
    let sceneInfo = Var.Create "-"
    let mathInfo = Var.Create "Press “Compute”."
    let pickInfo = Var.Create "Nothing picked yet."

    let orbitLabel = Var.Create "Auto-orbit: off"
    let directionalLabel = Var.Create "Directional light: on"
    let shadowLabel = Var.Create "Shadows: on"
    let wireframeLabel = Var.Create "Wireframe: off"
    let alphaLabel = Var.Create "Alpha: 1.0"
    let glowLabel = Var.Create "Glow: on"
    let pauseLabel = Var.Create "Pause"

    let labelText = Var.Create "Babylon.js 9"

    // Theme: light is the default; the choice is persisted in localStorage.
    // The storage key is shared with the bootstrap script in index.html.
    let themeStorageKey = "websharper-babylonjs-theme"
    let theme = Var.Create "light"
    let themeLabel = Var.Create "🌙 Dark mode"

    // ------------------------------------------------------------------
    // Scene state
    // ------------------------------------------------------------------

    type GalleryItem = { Mesh: AbstractMesh; Material: StandardMaterial }

    let mutable engine : Engine = null
    let mutable scene : Scene = null
    let mutable camera : ArcRotateCamera = null
    let mutable hemi : HemisphericLight = null
    let mutable dir : DirectionalLight = null
    let mutable shadows : ShadowGenerator = null
    let mutable glow : GlowLayer = null
    let mutable groundMat : StandardMaterial = null
    let mutable labelTex : DynamicTexture = null
    let mutable gallery : ResizeArray<GalleryItem> = ResizeArray()
    let mutable highlighted : AbstractMesh = null
    let mutable autoOrbit = false
    let mutable paused = false
    let mutable wireframe = false
    let mutable alpha = 1.0
    let mutable lightScale = 1.0
    let mutable paletteIndex = 0
    let mutable glowOn = true
    let mutable shadowsOn = true
    let mutable directionalOn = true
    let mutable frame = 0

    let palette = [| "#4f46e5"; "#0e7490"; "#d97706"; "#db2777"; "#15803d" |]

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    let byId (id: string) = JS.Document.GetElementById id

    let fmtVec (v: Vector3) =
        if isNull v then "null" else sprintf "%.2f, %.2f, %.2f" v.X v.Y v.Z

    let drawLabel (text: string) =
        if not (isNull labelTex) then
            let ctx = labelTex.GetContext()
            ctx.ClearRect(0., 0., 512., 512.)
            ctx.TextAlign <- WebSharper.JavaScript.CanvasTextAlign.Center
            ctx.TextBaseline <- WebSharper.JavaScript.CanvasTextBaseLine.Middle
            let color = if theme.Value = "dark" then "#e2e8f0" else "#1e293b"
            labelTex.DrawText(
                text,
                256,
                256,
                "bold 56px system-ui, sans-serif",
                color,
                null,
                true,
                true
            )

    let applyTheme () =
        JS.Document.DocumentElement.SetAttribute("data-theme", theme.Value)
        themeLabel.Value <-
            if theme.Value = "dark" then "☀️ Light mode" else "🌙 Dark mode"
        if not (isNull scene) then
            scene.ClearColor <-
                if theme.Value = "dark" then Color4(0.04, 0.07, 0.13, 1.)
                else Color4(0.96, 0.97, 0.99, 1.)
            if not (isNull groundMat) then
                groundMat.DiffuseColor <-
                    if theme.Value = "dark" then Color3.FromHexString "#1e293b"
                    else Color3.FromHexString "#e2e8f0"
            if not (isNull labelTex) then
                drawLabel labelText.Value

    let toggleTheme () =
        theme.Value <- (if theme.Value = "dark" then "light" else "dark")
        try JS.Window.LocalStorage.SetItem(themeStorageKey, theme.Value) with _ -> ()
        applyTheme ()

    let newMaterial name (hex: string) =
        let m = StandardMaterial(name, scene)
        m.DiffuseColor <- Color3.FromHexString hex
        m.SpecularColor <- Color3(0.12, 0.12, 0.12)
        m

    let addItem (mesh: AbstractMesh) (mat: StandardMaterial) =
        gallery.Add({ Mesh = mesh; Material = mat })
        if shadowsOn && not (isNull shadows) then
            shadows.AddShadowCaster(mesh) |> ignore
        mesh

    // ------------------------------------------------------------------
    // Render loop
    // ------------------------------------------------------------------

    let render () =
        if not (isNull scene) && not (isNull engine) then
            let dt = engine.GetDeltaTime()
            if autoOrbit then
                camera.Alpha <- camera.Alpha + dt * 0.00018
            for item in gallery do
                item.Mesh.Rotation.Y <- item.Mesh.Rotation.Y + dt * 0.0006
            scene.Render()
            frame <- frame + 1
            if frame % 15 = 0 then
                fps.Value <- sprintf "%.0f fps · %.1f ms/frame" (engine.GetFps()) dt
        ()

    // ------------------------------------------------------------------
    // Scene construction
    // ------------------------------------------------------------------

    let onPointerDown (info: PointerInfo) =
        if info.Type = PointerEventTypes.PointerDown then
            let pick = info.PickInfo
            if not (isNull pick) && pick.Hit then
                let mesh = pick.PickedMesh
                if not (isNull mesh) then
                    // Reset the previous highlight.
                    if not (isNull highlighted) then
                        for item in gallery do
                            if item.Mesh ===. highlighted then
                                item.Material.EmissiveColor <- Color3.Black()
                    // Highlight the picked mesh.
                    for item in gallery do
                        if item.Mesh ===. mesh then
                            item.Material.EmissiveColor <-
                                if theme.Value = "dark" then Color3(0.16, 0.19, 0.42)
                                else Color3(0.24, 0.22, 0.60)
                    highlighted <- mesh
                    let pt = pick.PickedPoint
                    pickInfo.Value <-
                        sprintf "Picked “%s” at (%s) · distance %.2f" mesh.Name (fmtVec pt) pick.Distance
                    status.Value <- sprintf "scene.Pick hit “%s”." mesh.Name
                else
                    highlighted <- null
                    pickInfo.Value <- "Picked the ground / nothing."
            else
                highlighted <- null
                pickInfo.Value <- "Nothing under the pointer."
        ()

    let createGround () =
        let mesh =
            MeshBuilder.CreateGround(
                "ground",
                GroundOptions(Width = 16., Height = 16., Subdivisions = 4),
                scene
            )
        groundMat <- StandardMaterial("groundMat", scene)
        groundMat.SpecularColor <- Color3.Black()
        let tex = DynamicTexture("groundTex", 512, scene, true)
        let ctx = tex.GetContext()
        ctx.FillStyle <- "#ffffff"
        ctx.FillRect(0., 0., 512., 512.)
        ctx.FillStyle <- "#e2e8f0"
        for i in 0 .. 3 do
            for j in 0 .. 3 do
                if (i + j) % 2 = 1 then
                    ctx.FillRect(float i * 128., float j * 128., 128., 128.)
        tex.UScale <- 3.5
        tex.VScale <- 3.5
        tex.Update()
        groundMat.DiffuseTexture <- tex
        mesh.Material <- groundMat
        mesh.ReceiveShadows <- true
        mesh

    let createGallery () =
        // Box
        let box =
            MeshBuilder.CreateBox("box", BoxOptions(Size = 1.7), scene)
        box.Position <- Vector3(-4.2, 1.0, 0.)
        let boxMat = newMaterial "boxMat" palette.[0]
        box.Material <- boxMat
        box.ReceiveShadows <- true
        addItem box boxMat |> ignore

        // Sphere with an emissive material (picked up by the glow layer)
        let sphere =
            MeshBuilder.CreateSphere(
                "sphere",
                SphereOptions(Diameter = 1.9, Segments = 32),
                scene
            )
        sphere.Position <- Vector3(-1.4, 1.2, 0.)
        let sphereMat = newMaterial "sphereMat" palette.[1]
        sphereMat.EmissiveColor <- Color3.FromHexString "#22d3ee"
        sphere.Material <- sphereMat
        sphere.ReceiveShadows <- true
        addItem sphere sphereMat |> ignore

        // Torus knot
        let knot =
            MeshBuilder.CreateTorusKnot(
                "knot",
                TorusKnotOptions(
                    Radius = 0.9,
                    Tube = 0.3,
                    RadialSegments = 128,
                    TubularSegments = 32
                ),
                scene
            )
        knot.Position <- Vector3(1.5, 1.4, 0.)
        let knotMat = newMaterial "knotMat" palette.[2]
        knot.Material <- knotMat
        knot.ReceiveShadows <- true
        addItem knot knotMat |> ignore

        // Cylinder
        let cylinder =
            MeshBuilder.CreateCylinder(
                "cylinder",
                CylinderOptions(Height = 1.8, DiameterTop = 0.4, DiameterBottom = 1.2, Tessellation = 32),
                scene
            )
        cylinder.Position <- Vector3(4.2, 0.9, 0.)
        let cylinderMat = newMaterial "cylinderMat" palette.[3]
        cylinder.Material <- cylinderMat
        cylinder.ReceiveShadows <- true
        addItem cylinder cylinderMat |> ignore

        // Ico sphere in the back row
        let ico =
            MeshBuilder.CreateIcoSphere(
                "ico",
                IcoSphereOptions(Radius = 0.9, Subdivisions = 2, Flat = true),
                scene
            )
        ico.Position <- Vector3(-3.4, 1.0, -3.4)
        let icoMat = newMaterial "icoMat" palette.[4]
        ico.Material <- icoMat
        ico.ReceiveShadows <- true
        addItem ico icoMat |> ignore

        // Capsule
        let capsule =
            MeshBuilder.CreateCapsule(
                "capsule",
                CapsuleOptions(Height = 2.0, Radius = 0.55, Tessellation = 24),
                scene
            )
        capsule.Position <- Vector3(-0.4, 1.1, -3.6)
        let capsuleMat = newMaterial "capsuleMat" palette.[0]
        capsule.Material <- capsuleMat
        capsule.ReceiveShadows <- true
        addItem capsule capsuleMat |> ignore

        // Disc
        let disc =
            MeshBuilder.CreateDisc(
                "disc",
                DiscOptions(Radius = 0.8, Tessellation = 48),
                scene
            )
        disc.Position <- Vector3(2.8, 1.2, -3.4)
        disc.Rotation.X <- -1.2
        let discMat = newMaterial "discMat" palette.[1]
        disc.Material <- discMat
        addItem disc discMat |> ignore

        // Torus
        let torus =
            MeshBuilder.CreateTorus(
                "torus",
                TorusOptions(Diameter = 1.6, Thickness = 0.35, Tessellation = 32),
                scene
            )
        torus.Position <- Vector3(4.6, 1.1, -3.2)
        let torusMat = newMaterial "torusMat" palette.[2]
        torus.Material <- torusMat
        torus.ReceiveShadows <- true
        addItem torus torusMat |> ignore

        // Tube along a path
        let path =
            [|
                Vector3(-4.6, 0.3, 4.0)
                Vector3(-2.6, 0.9, 4.4)
                Vector3(-0.4, 0.4, 4.0)
                Vector3(1.8, 0.9, 4.4)
                Vector3(4.2, 0.3, 4.0)
            |]
        let tube =
            MeshBuilder.CreateTube(
                "tube",
                TubeOptions(path, Radius = 0.18, Tessellation = 20),
                scene
            )
        let tubeMat = newMaterial "tubeMat" palette.[3]
        tube.Material <- tubeMat
        addItem tube tubeMat |> ignore

        // Ribbon
        let ribbonPaths =
            [|
                [| Vector3(-4.5, 0.05, -1.6); Vector3(-2.5, 0.7, -1.6); Vector3(-0.5, 0.05, -1.6) |]
                [| Vector3(-4.5, 0.05, -0.9); Vector3(-2.5, 1.1, -0.9); Vector3(-0.5, 0.05, -0.9) |]
            |]
        let ribbon =
            MeshBuilder.CreateRibbon(
                "ribbon",
                RibbonOptions(ribbonPaths, SideOrientation = 2),
                scene
            )
        let ribbonMat = newMaterial "ribbonMat" palette.[4]
        ribbonMat.BackFaceCulling <- false
        ribbon.Material <- ribbonMat
        addItem ribbon ribbonMat |> ignore

        // Lathe from a profile
        let profile =
            [|
                Vector3(0.0, 0.0, 0.0)
                Vector3(0.7, 0.0, 0.0)
                Vector3(0.95, 0.5, 0.0)
                Vector3(0.65, 1.0, 0.0)
                Vector3(0.25, 1.4, 0.0)
            |]
        let lathe =
            MeshBuilder.CreateLathe(
                "lathe",
                LatheOptions(profile, Tessellation = 32, Cap = 0),
                scene
            )
        lathe.Position <- Vector3(1.2, 0.05, 3.2)
        let latheMat = newMaterial "latheMat" palette.[0]
        lathe.Material <- latheMat
        lathe.ReceiveShadows <- true
        addItem lathe latheMat |> ignore

        // Extruded shape
        let shape =
            [|
                Vector3(-0.5, 0.0, 0.0)
                Vector3(0.0, 0.9, 0.0)
                Vector3(0.5, 0.0, 0.0)
            |]
        let extrudePath = [| Vector3(0.0, 0.0, 0.0); Vector3(0.0, 0.0, 1.2) |]
        let extruded =
            MeshBuilder.ExtrudeShape(
                "extruded",
                ExtrudeShapeOptions(shape, extrudePath, Cap = 2),
                scene
            )
        extruded.Position <- Vector3(-1.6, 0.05, 2.6)
        let extrudedMat = newMaterial "extrudedMat" palette.[1]
        extruded.Material <- extrudedMat
        addItem extruded extrudedMat |> ignore

        // Line system
        let points =
            [|
                Vector3(-5.5, 0.02, -5.5)
                Vector3(-3.0, 0.6, -5.0)
                Vector3(-1.0, 0.02, -5.6)
                Vector3(1.5, 0.8, -5.0)
                Vector3(5.5, 0.02, -5.5)
            |]
        let lines =
            MeshBuilder.CreateLines("lines", LinesOptions(points), scene)
        lines.Color <- Color3.FromHexString palette.[2]

        // Label plane, billboarded toward the camera
        let labelMesh =
            MeshBuilder.CreatePlane(
                "label",
                PlaneOptions(Width = 2.6, Height = 2.6),
                scene
            )
        labelMesh.Position <- Vector3(0., 4.4, -0.5)
        labelMesh.BillboardMode <- 7
        labelTex <- DynamicTexture("labelTex", 512, scene, true)
        labelTex.HasAlpha <- true
        let labelMat = StandardMaterial("labelMat", scene)
        labelMat.DiffuseTexture <- labelTex
        labelMat.UseAlphaFromDiffuseTexture <- true
        labelMat.DisableLighting <- true
        labelMat.DiffuseColor <- Color3.White()
        labelMat.SpecularColor <- Color3.Black()
        labelMat.BackFaceCulling <- false
        labelMesh.Material <- labelMat
        drawLabel labelText.Value

    let rec buildScene () =
        let canvas = As<HTMLCanvasElement> (byId "scene")
        engine <-
            Engine(
                canvas,
                true,
                EngineOptions(PreserveDrawingBuffer = true, Stencil = true)
            )
        scene <- Scene(engine)
        camera <-
            ArcRotateCamera("camera", -1.25, 1.05, 14., Vector3(0., 1.2, 0.), scene)
        camera.LowerRadiusLimit <- 4.
        camera.UpperRadiusLimit <- 32.
        camera.WheelPrecision <- 12.
        camera.PanningSensibility <- 200.
        camera.AttachControl(canvas, true)

        hemi <- HemisphericLight("hemi", Vector3(0., 1., 0.), scene)
        hemi.Intensity <- 0.85
        hemi.GroundColor <- Color3.FromHexString "#ffffff"

        dir <- DirectionalLight("dir", Vector3(-0.4, -1., -0.35), scene)
        dir.Position <- Vector3(8., 14., 8.)
        dir.Intensity <- 0.9
        dir.Diffuse <- Color3.FromHexString "#fbbf24"

        shadows <- ShadowGenerator(1024, dir)
        shadows.UseBlurExponentialShadowMap <- true
        shadows.BlurKernel <- 32
        shadows.Darkness <- 0.35

        glow <-
            GlowLayer(
                "glow",
                scene,
                IGlowLayerOptions(BlurKernelSize = 32, MainTextureFixedSize = 256)
            )
        glow.Intensity <- 0.9

        scene.OnPointerObservable.Add(fun info -> onPointerDown info) |> ignore

        createGround () |> ignore
        createGallery ()

        applyTheme ()
        engine.RunRenderLoop render

        engineInfo.Value <-
            sprintf "Babylon.js 9 · %d×%d · %s" (engine.GetRenderWidth()) (engine.GetRenderHeight()) "WebGL2"
        fps.Value <- sprintf "%.0f fps" (engine.GetFps())
        updateCameraInfo ()
        updateLightInfo ()
        updateMaterialInfo ()
        sceneInfo.Value <-
            sprintf "%d meshes · %d lights · %d materials" scene.Meshes.Length scene.Lights.Length scene.Materials.Length
        // Constructing a Ray imports Babylon's `Culling/ray` module, which
        // `scene.Pick` and `onPointerObservable` pick info rely on. The ray is
        // then used for a first probe straight down onto the box.
        let start = Vector3(-4.2, 8., 0.)
        let downward = Ray(start, Vector3.Down(), 50.)
        let probe = scene.PickWithRay(downward)
        pickInfo.Value <-
            if probe.Hit && not (isNull probe.PickedMesh) then
                sprintf
                    "Ray from (%s) hits “%s” at (%s)"
                    (fmtVec start)
                    probe.PickedMesh.Name
                    (fmtVec probe.PickedPoint)
            else
                sprintf "Ray from (%s) hits nothing" (fmtVec start)
        status.Value <- "Scene ready — drive it from the controls."

    // ------------------------------------------------------------------
    // Camera controls
    // ------------------------------------------------------------------

    and updateCameraInfo () =
        cameraInfo.Value <-
            sprintf "alpha %.2f · beta %.2f · radius %.1f" camera.Alpha camera.Beta camera.Radius

    and updateLightInfo () =
        lightInfo.Value <-
            sprintf
                "hemispheric %.2f · directional %.2f · shadows %s"
                hemi.Intensity
                dir.Intensity
                (if shadowsOn then "on" else "off")

    and updateMaterialInfo () =
        materialInfo.Value <-
            sprintf
                "wireframe %s · alpha %.2f · glow %.1f · %d gallery meshes"
                (if wireframe then "on" else "off")
                alpha
                glow.Intensity
                gallery.Count

    let resetView () =
        camera.Alpha <- -1.25
        camera.Beta <- 1.05
        camera.Radius <- 14.
        camera.SetTarget(Vector3(0., 1.2, 0.))
        updateCameraInfo ()
        status.Value <- "Camera reset."

    let orbitIn () =
        camera.Radius <- max 4. (camera.Radius - 2.)
        updateCameraInfo ()

    let orbitOut () =
        camera.Radius <- min 32. (camera.Radius + 2.)
        updateCameraInfo ()

    let viewFront () =
        camera.Alpha <- -System.Math.PI / 2.
        camera.Beta <- 1.2
        updateCameraInfo ()

    let viewSide () =
        camera.Alpha <- 0.
        camera.Beta <- 1.2
        updateCameraInfo ()

    let viewTop () =
        camera.Beta <- 0.08
        updateCameraInfo ()

    let toggleOrbit () =
        autoOrbit <- not autoOrbit
        orbitLabel.Value <-
            if autoOrbit then "Auto-orbit: on" else "Auto-orbit: off"

    // ------------------------------------------------------------------
    // Light controls
    // ------------------------------------------------------------------

    let changeLights (delta: float) =
        lightScale <- max 0.1 (min 2.0 (lightScale + delta))
        hemi.Intensity <- 0.85 * lightScale
        dir.Intensity <- 0.9 * lightScale
        updateLightInfo ()

    let brighter () = changeLights 0.2
    let dimmer () = changeLights -0.2

    let warmLight () =
        dir.Diffuse <- Color3.FromHexString "#fbbf24"
        hemi.GroundColor <- Color3.FromHexString "#fef3c7"
        status.Value <- "Warm light palette."

    let coolLight () =
        dir.Diffuse <- Color3.FromHexString "#93c5fd"
        hemi.GroundColor <- Color3.FromHexString "#e0f2fe"
        status.Value <- "Cool light palette."

    let toggleDirectional () =
        directionalOn <- not directionalOn
        dir.SetEnabled directionalOn
        directionalLabel.Value <-
            if directionalOn then "Directional light: on" else "Directional light: off"
        updateLightInfo ()

    let toggleShadows () =
        shadowsOn <- not shadowsOn
        dir.ShadowEnabled <- shadowsOn
        shadowLabel.Value <- if shadowsOn then "Shadows: on" else "Shadows: off"
        updateLightInfo ()

    // ------------------------------------------------------------------
    // Material controls
    // ------------------------------------------------------------------

    let cyclePalette () =
        paletteIndex <- (paletteIndex + 1) % palette.Length
        let hex = palette.[paletteIndex]
        for item in gallery do
            item.Material.DiffuseColor <- Color3.FromHexString hex
        updateMaterialInfo ()
        status.Value <- sprintf "Diffuse palette: %s." hex

    let toggleWireframe () =
        wireframe <- not wireframe
        for item in gallery do
            item.Material.Wireframe <- wireframe
        wireframeLabel.Value <-
            if wireframe then "Wireframe: on" else "Wireframe: off"
        updateMaterialInfo ()

    let toggleAlpha () =
        alpha <- (if alpha > 0.99 then 0.55 else 1.0)
        for item in gallery do
            item.Material.Alpha <- alpha
        alphaLabel.Value <- sprintf "Alpha: %.2f" alpha
        updateMaterialInfo ()

    let toggleGlow () =
        glowOn <- not glowOn
        glow.Intensity <- if glowOn then 0.9 else 0.
        glowLabel.Value <- if glowOn then "Glow: on" else "Glow: off"
        updateMaterialInfo ()

    // ------------------------------------------------------------------
    // Shape controls
    // ------------------------------------------------------------------

    let mutable shapeCounter = 0

    let nextName prefix =
        shapeCounter <- shapeCounter + 1
        prefix + "-" + string shapeCounter

    let placeItem (mesh: AbstractMesh) (mat: StandardMaterial) =
        let angle = float shapeCounter * 0.9
        mesh.Position <-
            Vector3(5.2 * System.Math.Cos angle, 0.9, 5.2 * System.Math.Sin angle)
        mesh.Material <- mat
        mesh.ReceiveShadows <- true
        addItem mesh mat |> ignore
        sceneInfo.Value <- sprintf "%d meshes in the scene" gallery.Count
        status.Value <- sprintf "Added “%s”." mesh.Name

    let addBox () =
        let mesh =
            MeshBuilder.CreateBox(
                nextName "box",
                BoxOptions(Size = 1.1, Updatable = true),
                scene
            )
        placeItem mesh (newMaterial (nextName "boxMat") palette.[paletteIndex])

    let addSphere () =
        let mesh =
            MeshBuilder.CreateSphere(
                nextName "sphere",
                SphereOptions(Diameter = 1.2, Segments = 24),
                scene
            )
        let mat = newMaterial (nextName "sphereMat") palette.[(paletteIndex + 2) % palette.Length]
        mat.EmissiveColor <- Color3.FromHexString "#a5b4fc"
        placeItem mesh mat

    let addTorus () =
        let mesh =
            MeshBuilder.CreateTorusKnot(
                nextName "knot",
                TorusKnotOptions(Radius = 0.6, Tube = 0.2, RadialSegments = 96, TubularSegments = 24),
                scene
            )
        placeItem mesh (newMaterial (nextName "knotMat") palette.[(paletteIndex + 3) % palette.Length])

    let removeLast () =
        if gallery.Count > 0 then
            let item = gallery.[gallery.Count - 1]
            gallery.RemoveAt(gallery.Count - 1)
            item.Mesh.Dispose()
            item.Material.Dispose()
            sceneInfo.Value <- sprintf "%d meshes in the scene" gallery.Count
            status.Value <- "Removed the last gallery mesh."

    let togglePause () =
        paused <- not paused
        if paused then
            engine.StopRenderLoop render
            pauseLabel.Value <- "Resume"
            status.Value <- "Render loop stopped."
        else
            engine.RunRenderLoop render
            pauseLabel.Value <- "Pause"
            status.Value <- "Render loop resumed."

    let resetScene () =
        if not (isNull engine) then
            engine.StopRenderLoop render
        if not (isNull scene) then
            scene.Dispose()
        if not (isNull engine) then
            engine.Dispose()
        gallery <- ResizeArray()
        highlighted <- null
        shapeCounter <- 0
        paletteIndex <- 0
        wireframe <- false
        alpha <- 1.0
        lightScale <- 1.0
        glowOn <- true
        shadowsOn <- true
        directionalOn <- true
        paused <- false
        autoOrbit <- false
        pauseLabel.Value <- "Pause"
        orbitLabel.Value <- "Auto-orbit: off"
        wireframeLabel.Value <- "Wireframe: off"
        alphaLabel.Value <- "Alpha: 1.0"
        glowLabel.Value <- "Glow: on"
        shadowLabel.Value <- "Shadows: on"
        directionalLabel.Value <- "Directional light: on"
        pickInfo.Value <- "Nothing picked yet."
        buildScene ()

    // ------------------------------------------------------------------
    // Dynamic texture & API tour
    // ------------------------------------------------------------------

    let applyLabel () =
        drawLabel labelText.Value
        status.Value <- sprintf "Label set to “%s”." labelText.Value

    let readApi () =
        // TEMP DEBUG
        let dbg = scene.Pick(497., 280.)
        mathInfo.Value <-
            sprintf
                "DBG pick hit=%b mesh=%s meshes=%d"
                dbg.Hit
                (if isNull dbg.PickedMesh then "null" else dbg.PickedMesh.Name)
                scene.Meshes.Length
        let up = Vector3.Up()
        let right = Vector3.Right()
        let cross = Vector3.Cross(up, right)
        let dist = Vector3.Distance(Vector3(0., 0., 0.), Vector3(3., 4., 0.))
        let c = Color3.FromHexString "#4f46e5"
        let lum = c.ToLuminance()
        let m = Matrix.RotationY(System.Math.PI / 4.)
        let q = Quaternion.FromEulerAngles(0.2, 0.4, 0.1)
        let e = q.ToEulerAngles()
        mathInfo.Value <-
            sprintf
                "cross(up, right) = (%s)\n|(3, 4, 0)| = %.1f\n#4f46e5 → %s · luminance %.3f\nrotationY(π/4) determinant = %.3f\neuler(0.2, 0.4, 0.1) = (%s)\nrender %d×%d · hardware scale %.2f"
                (fmtVec cross)
                dist
                (c.ToHexString())
                lum
                (m.Determinant())
                (fmtVec e)
                (engine.GetRenderWidth())
                (engine.GetRenderHeight())
                (engine.GetHardwareScalingLevel())
        let forward = camera.GetForwardRay()
        let probeInfo =
            sprintf
                "forward ray (%s) → %s"
                (fmtVec forward.Origin)
                (fmtVec forward.Direction)
        status.Value <- sprintf "Tour computed · camera.GetForwardRay: %s" probeInfo

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------

    [<SPAEntryPoint>]
    let Main () =
        // Restore a previously chosen theme (light is the default).
        let savedTheme =
            try JS.Window.LocalStorage.GetItem(themeStorageKey) with _ -> null
        if savedTheme = "dark" || savedTheme = "light" then
            theme.Value <- savedTheme
        applyTheme ()

        // `.Bind()` renders the whole document body, so the canvas below is in
        // the DOM only after this call.
        IndexTemplate()
            .status(status.V)
            .notice(notice.V)
            .engineInfo(engineInfo.V)
            .fps(fps.V)
            .cameraInfo(cameraInfo.V)
            .lightInfo(lightInfo.V)
            .materialInfo(materialInfo.V)
            .sceneInfo(sceneInfo.V)
            .mathInfo(mathInfo.V)
            .pickInfo(pickInfo.V)
            .orbitLabel(orbitLabel.V)
            .directionalLabel(directionalLabel.V)
            .shadowLabel(shadowLabel.V)
            .wireframeLabel(wireframeLabel.V)
            .alphaLabel(alphaLabel.V)
            .glowLabel(glowLabel.V)
            .pauseLabel(pauseLabel.V)
            .themeLabel(themeLabel.V)
            .LabelText(labelText)
            .ToggleTheme(fun _ -> toggleTheme ())
            .ResetView(fun _ -> resetView ())
            .OrbitIn(fun _ -> orbitIn ())
            .OrbitOut(fun _ -> orbitOut ())
            .ViewFront(fun _ -> viewFront ())
            .ViewSide(fun _ -> viewSide ())
            .ViewTop(fun _ -> viewTop ())
            .ToggleOrbit(fun _ -> toggleOrbit ())
            .Brighter(fun _ -> brighter ())
            .Dimmer(fun _ -> dimmer ())
            .WarmLight(fun _ -> warmLight ())
            .CoolLight(fun _ -> coolLight ())
            .ToggleDirectional(fun _ -> toggleDirectional ())
            .ToggleShadows(fun _ -> toggleShadows ())
            .CyclePalette(fun _ -> cyclePalette ())
            .ToggleWireframe(fun _ -> toggleWireframe ())
            .ToggleAlpha(fun _ -> toggleAlpha ())
            .ToggleGlow(fun _ -> toggleGlow ())
            .AddBox(fun _ -> addBox ())
            .AddSphere(fun _ -> addSphere ())
            .AddTorus(fun _ -> addTorus ())
            .RemoveLast(fun _ -> removeLast ())
            .TogglePause(fun _ -> togglePause ())
            .ResetScene(fun _ -> resetScene ())
            .ApplyLabel(fun _ -> applyLabel ())
            .ReadApi(fun _ -> readApi ())
            .Bind()

        JS.Window.AddEventListener("resize", (fun () ->
            if not (isNull engine) then engine.Resize()
        ))

        buildScene ()