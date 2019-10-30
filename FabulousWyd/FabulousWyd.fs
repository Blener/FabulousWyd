// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace FabulousWyd

open Domain

open FSharp.Data.Runtime
open System.Diagnostics
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms

module App =
    let shellRef = ViewRef<Shell>()
    
    type Model = 
      { Index: Index list
        Maps: Map list
        Mobs: Mob list }

    let mapCmdToMsg sqlPath cmd =
        match cmd with
        | LoadIndex -> Index.LoadIndex sqlPath |> Cmd.ofAsyncMsg
        | LoadMaps maps -> Map.LoadMaps sqlPath maps |> Cmd.ofAsyncMsg
        | LoadMobs mobs -> Mob.LoadMobs sqlPath mobs |> Cmd.ofAsyncMsg
    
    let initModel = { Index = []; Maps = []; Mobs = [] }

    let init () = initModel, [ LoadIndex ]

    let update msg (model: Model) =
        match msg with
        | IndexLoaded indexes ->
            { model with Index = indexes }, [ LoadMaps (indexes |> List.filter (fun x -> x.Type = "map") |> List.map (fun x -> x.Name)) ]
        | MapsLoaded maps ->
            { model with Maps = maps }, [LoadMobs (model.Index |> List.filter (fun x -> x.Type = "mob") |> List.map (fun x -> x.Name))]
        | MobsLoaded mobs ->
            { model with Mobs = mobs }, []
        
        | ReloadIndexes -> { model with Index = [] }, [ LoadIndex ]
        | ReloadMapas -> { model with Maps = [] }, [ LoadMaps (model.Index |> List.filter (fun x -> x.Type = "map") |> List.map (fun x -> x.Name)) ]
        | ReloadMobs -> { model with Mobs = [] }, [ LoadMobs (model.Index |> List.filter (fun x -> x.Type = "mob") |> List.map (fun x -> x.Name)) ]

    let view (model: Model) dispatch =
        Shell.shell [
            Shell.Ref shellRef
            Shell.Items [
                yield FlyoutItem.flyoutItem [
                    FlyoutItem.Title "Página Inicial"
                    FlyoutItem.Items [
                        Tab.tab [
                            Tab.Title "Inicio"
                            Tab.Items [
                                ShellContent.shellContent [
                                    ShellContent.Content <|
                                        ContentPage.contentPage [
                                            ContentPage.Content <|
                                                Grid.grid [
                                                    Grid.Rows [ GridLength.Auto; GridLength.Auto ]
                                                    Grid.Columns [ for _ in 1..3 -> GridLength.Auto ]
                                                    Grid.Children [
                                                        Label.label [
                                                            Label.GridRow 0
                                                            Label.GridColumn 1
                                                            Label.GridColumnSpan 3
                                                            Label.Text <| sprintf "Status: Index - %i; Mapas - %i; Mobs - %i" model.Index.Length model.Maps.Length model.Mobs.Length
                                                        ]
                                                        Label.label [
                                                            Label.GridRow 1
                                                            Label.GridColumn 1
                                                            Label.GridColumnSpan 3
                                                            Label.Text <|
                                                                if model.Index.Length = 0 then "Carregando indexes, por favor aguarde..."
                                                                elif model.Maps.Length = 0 then "Carregando mapas, por favor aguarde..."
                                                                elif model.Mobs.Length = 0 then "Carregando mobs, por favor aguarde..."
                                                                else ""
                                                        ]
                                                    ]
                                                ]
                                                
                                        ]
                                ]
                            ]
                        ]
                    ]
                ]
                for i in model.Maps do
                    yield FlyoutItem.flyoutItem [
                        FlyoutItem.Title i.Name
                        FlyoutItem.Items [
                            Tab.tab [
                                Tab.Title "Mobs"
                                Tab.Route <| sprintf "%sMob" i.Name
                                Tab.Items [
                                    ShellContent.shellContent [
                                        ShellContent.Content <|
                                            ContentPage.contentPage [
                                                ContentPage.Content <|
                                                    ScrollView.scrollView [
                                                        ScrollView.Content <|
                                                            StackLayout.stackLayout [
                                                                StackLayout.Children [
                                                                    for m in i.Mobs do
                                                                        let mob = model.Mobs |> List.tryFind (fun x -> x.Name = m.Name)
                                                                        match mob with
                                                                        | None -> yield ActivityIndicator.activityIndicator [ ActivityIndicator.IsRunning true ]
                                                                        | Some m ->
                                                                            if not m.Drops.IsEmpty then
                                                                                yield Label.label [ Label.Text m.Name ]
                                                                                for d in m.Drops do
                                                                                    let raridade =
                                                                                        match d.Rarity with
                                                                                        | "veryeasy" -> "Muito Fácil"
                                                                                        | "easy" -> "Fácil"
                                                                                        | "medium" -> "Mediano"
                                                                                        | "hard" -> "Dificil"
                                                                                        | "rare" -> "Raro"
                                                                                        | _ -> ""
                                                                                    yield Label.label [ Label.Text <| sprintf "%s - %s" d.Name raridade; Label.TextColor Color.Green ]
                                                                ]
                                                            ]
                                                    ]
                                            ]
                                    ]
                                ]
                            ]
                        ]
                    ]
            ]
        ]

    // Note, this declaration is needed if you enable LiveUpdate
    let program mapCmdToMsg = Program.mkProgramWithCmdMsg init update view mapCmdToMsg

type App (sqlPath) as app = 
    inherit Application ()

    let sqlPath = System.IO.Path.Combine(sqlPath, "FabulousWyd.db")
    let mapCmdToMsg = App.mapCmdToMsg sqlPath
    
    let runner = 
        App.program mapCmdToMsg
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    //do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


