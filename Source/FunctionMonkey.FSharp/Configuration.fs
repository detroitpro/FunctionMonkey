﻿namespace FunctionMonkey.FSharp
open FunctionMonkey.Abstractions
open FunctionMonkey.Abstractions.Builders
open FunctionMonkey.Abstractions.Builders.Model
open FunctionMonkey.Abstractions.Http
open FunctionMonkey.Commanding.Abstractions.Validation
open FunctionMonkey.Model
open System
open System.Net.Http

module Configuration =
    exception ConfigurationException
    
    type IFunctionHandler = interface end
    type Handler<'commandType, 'commandResult> =
        {        
            handler: 'commandType -> 'commandResult
        }
        interface IFunctionHandler
        
    type OutputAuthoredSource =
        | Path of string
        | NoSourceOutput
        
    type FunctionCompilerMetadata =
         {
             functionDefinitions: AbstractFunctionDefinition list
             openApiConfiguration: OpenApiConfiguration
             outputAuthoredSourceFolder: OutputAuthoredSource
         }
         interface IFunctionCompilerMetadata with
            member i.FunctionDefinitions = i.functionDefinitions :> System.Collections.Generic.IReadOnlyCollection<AbstractFunctionDefinition>
            member i.OpenApiConfiguration = i.openApiConfiguration
            member i.OutputAuthoredSourceFolder = match i.outputAuthoredSourceFolder with | Path p -> p | NoSourceOutput -> null
        
    type HttpVerb =
            | Get
            | Put
            | Post
            | Patch
            | Delete
            //| Custom of string
    type HttpRoute =
        | Path of string
        | Unspecified
    type HttpFunction =
        {
            commandType: Type
            resultType: Type
            handler: IFunctionHandler
            verbs: HttpVerb list
            route: HttpRoute
        }
        
    type Authorization =
        {
            defaultAuthorizationMode: AuthorizationTypeEnum
            defaultAuthorizationHeader: string
        }
    
    type Functions = {
        httpFunctions: HttpFunction list 
    }
    
    type FunctionAppConfiguration = {
        authorization: Authorization       
        functions: Functions
    }
    
    let private defaultAuthorization = {
        defaultAuthorizationMode = AuthorizationTypeEnum.Function
        defaultAuthorizationHeader = "Bearer"
    }
    
    let private defaultFunctions = {
        httpFunctions = []
    }
    
    let private defaultFunctionAppConfiguration = {
        authorization = defaultAuthorization
        functions = defaultFunctions
    }
    
    let private combineRoutes firstPart secondPart =
        match firstPart with
        | Unspecified -> secondPart
        | Path p -> match secondPart with
                    | Unspecified -> firstPart
                    | Path p2 -> Path(p + p2)

    let private createFunctionCompilerMetadata configuration =
        let createHttpFunctionDefinition (configuration:FunctionAppConfiguration) httpFunction =
            let convertVerb verb =
                match verb with
                | Get -> HttpMethod.Get
                | Put -> HttpMethod.Put
                | Post -> HttpMethod.Post
                | Patch -> HttpMethod.Patch
                | Delete -> HttpMethod.Delete
                
            let httpFunctionDefinition =
                HttpFunctionDefinition(
                    httpFunction.commandType,
                    Verbs = System.Collections.Generic.HashSet(httpFunction.verbs |> Seq.map convertVerb),
                    Authorization = new System.Nullable<AuthorizationTypeEnum>(configuration.authorization.defaultAuthorizationMode),
                    ValidatesToken = (configuration.authorization.defaultAuthorizationMode = AuthorizationTypeEnum.TokenValidation),
                    TokenHeader = configuration.authorization.defaultAuthorizationHeader,
                    ClaimsPrincipalAuthorizationType = null,
                    HeaderBindingConfiguration = null,
                    HttpResponseHandlerType = null,
                    IsValidationResult = (not (httpFunction.resultType = typedefof<unit>) && typedefof<ValidationResult>.IsAssignableFrom(httpFunction.resultType)),
                    IsStreamCommand = false,
                    TokenValidatorType = null
                )
            
            httpFunctionDefinition :> AbstractFunctionDefinition
        
        {
            outputAuthoredSourceFolder = NoSourceOutput
            openApiConfiguration = OpenApiConfiguration()
            functionDefinitions =
                [] |> 
                Seq.append (configuration.functions.httpFunctions |> Seq.map (fun f -> createHttpFunctionDefinition configuration f))
                |> Seq.toList
                
        } :> IFunctionCompilerMetadata
    
    type azureFunction private() =
        static member inline http<'commandType, 'commandResultType> (handler:'commandType -> 'commandResultType, verb, ?subRoute) =
             {
                 verbs = [verb]
                 route = (match subRoute with | Some r -> Path(r) | None -> Unspecified)
                 handler = { handler = handler }
                 commandType = typedefof<'commandType>
                 resultType = typedefof<'commandResultType>
             }
        static member inline http<'commandType> (handler:'commandType -> unit , verb, ?subRoute) =
            azureFunction.http<'commandType, unit> (handler, verb, ?subRoute = subRoute)
                        
    type FunctionAppConfigurationBuilder() =
        member __.Yield (_: 'a) : FunctionAppConfiguration = defaultFunctionAppConfiguration
        member __.Run (configuration: FunctionAppConfiguration) =
            createFunctionCompilerMetadata configuration
        
        [<CustomOperation("httpRoute")>]
        member this.httpRoute(configuration:FunctionAppConfiguration, prefix, x) =
            { configuration
                with functions = {
                    configuration.functions
                        with httpFunctions = x
                            |> Seq.map (fun f -> { f with route = (combineRoutes (Path(prefix)) f.route) })
                            |> Seq.append configuration.functions.httpFunctions
                            |> Seq.toList
                }
            }
        
    let functionApp = FunctionAppConfigurationBuilder()