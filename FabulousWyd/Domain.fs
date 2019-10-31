module Domain

open System
open System.Diagnostics
open FSharp.Data
open LiteDB
open LiteDB.FSharp
open Newtonsoft.Json
open Xamarin.Forms

let ApiAll = "https://wydmisc.raidhut.com.br/droplist/global-br/data/all.json"
let ApiAllMaps = "https://wydmisc.raidhut.com.br/droplist/global-br/data/map.json"
let ApiType type' index = sprintf "https://wydmisc.raidhut.com.br/droplist/global-br/data/%s/%s.json" type' index

type IndexId = int
type ItemId = int
type MobId = int
type MapId = int
type MapName = string
type MobName = string

[<CLIMutable>]
type Index =
    { Id: IndexId
      Type: string
      Name: string }

[<CLIMutable>]
type Item =
    { Id: ItemId
      Index: int
      Name: string
      Rarity: string
      RarityLevel: int }
    member this.RarityColor =
        match this.Rarity with
        | "veryeasy" -> Color.Black
        | "easy" -> Color.Green
        | "medium" -> Color.DarkBlue
        | "hard" -> Color.DarkRed
        | "rare" -> Color.Purple
        | _ -> Color.Transparent
    
type MapPosition =
    { x: int
      y: int }
    
[<CLIMutable>]
type Mob =
    { Id: MobId
      Name: MobName
      Level: int
      Hp: int
      Mp: int
      Exp: int
      Maps: Index list
      Drops: Item list
      Pos: MapPosition list }

[<CLIMutable>]
type Map =
    { Id: MapId
      Name: MapName
      Mobs: Index list }

type Msg =
    | IndexLoaded of Index list
    | MapsLoaded of Map list
    | MobsLoaded of Mob list
    | ReloadFromServer
    | DatabaseErased
    
type Cmd =
    | LoadIndex
    | LoadMaps of MapName list
    | LoadMobs of MobName list
    | EraseDabatase
    
let fetchFromServer url =
    Http.AsyncRequestString(url)
    
let GetDb (sqlPath:string) =
    let mapper = FSharpBsonMapper()
    use db = new LiteDatabase(sqlPath, mapper)
    db
    
let GetCollection<'a> (sqlPath:string) =
    let db = GetDb sqlPath
    db.GetCollection<'a>()
        
let Upsert<'a> (sqlPath:string) (item:'a) =
    let collection = GetCollection<'a> sqlPath
    collection.Upsert item
    
let InsertAll<'a> (sqlPath:string) (items: 'a list) =
    let collection = GetCollection<'a> sqlPath
    collection.InsertBulk items

let GetAll<'a> (sqlPath:string) =
    let collection = GetCollection<'a> sqlPath
    collection.FindAll()
    
let GetOneMaybe<'a> (sqlPath:string) (id:string) =
    let collection = GetCollection<'a> sqlPath
    let bsonValue = BsonValue id
    let obj = collection.FindById bsonValue
    match box obj with
    | null -> None
    | _ -> Some obj
    
let EraseDatabase (sqlPath:string) =
    let db = GetDb sqlPath
    db.GetCollectionNames() |> Seq.map db.DropCollection |> ignore
    
module Index =
    let LoadIndex (sqlPath:string) : Async<Msg> =
        async {
            do! Async.SwitchToThreadPool()
            let dbIndex = GetAll<Index> sqlPath
            match dbIndex |> Seq.isEmpty with
            | false -> return dbIndex |> List.ofSeq |> IndexLoaded
            | true ->
                let! serverJson = fetchFromServer ApiAll
                let indexes = JsonConvert.DeserializeObject<Index list> serverJson
                do InsertAll sqlPath indexes |> ignore
                return IndexLoaded indexes
        }
        
module Mob =
    let LoadMob (mob:string) : Async<Mob> =
        async {
            do! Async.SwitchToThreadPool()
            Debug.WriteLine(sprintf "Loading mob %s" mob)
            let! serverJson = ApiType "mob" mob |> fetchFromServer
            return JsonConvert.DeserializeObject<Mob> serverJson
        }
        
    let LoadMobs (sqlPath:string) (mobs: string list) : Async<Msg> =
        async {
            do! Async.SwitchToThreadPool()
            let dbMobs = GetAll<Mob> sqlPath
            match dbMobs |> Seq.isEmpty with
            | false -> return dbMobs |> List.ofSeq |> MobsLoaded
            | true ->
                let mobTasks = mobs |> List.map LoadMob |> List.map Async.RunSynchronously
                do InsertAll sqlPath mobTasks |> ignore
                return MobsLoaded mobTasks
        }
    
module Map =
    let LoadMap (map:string) : Async<Map> =
        async {
            do! Async.SwitchToThreadPool()
            let! serverJson = ApiType "map" map |> fetchFromServer
            return JsonConvert.DeserializeObject<Map> serverJson
        }
        
    let LoadMaps (sqlPath:string) (maps: string list) : Async<Msg> =
        async {
            do! Async.SwitchToThreadPool()
            let dbMaps = GetAll<Map> sqlPath
            match dbMaps |> Seq.isEmpty with
            | false -> return dbMaps |> List.ofSeq |> MapsLoaded
            | true ->
                let mapTasks = maps |> List.map LoadMap
                let fullMaps = mapTasks |> Async.Parallel |> Async.RunSynchronously |> List.ofArray
                do InsertAll sqlPath fullMaps |> ignore
                return MapsLoaded fullMaps
        }