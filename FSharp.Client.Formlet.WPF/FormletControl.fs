﻿(* Copyright 1999-2005 The Apache Software Foundation or its licensors, as
 * applicable.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *)

namespace FSharp.Client.Formlet.WPF

open System.Collections
open System.Windows

open FSharp.Client.Formlet.Core

open Elements
open Controls

type FormletDispatchAction =
    | Rebuild   = 1
    | Submit    = 2
    | Reset     = 3

type FormletContext () =
    interface IFormletContext with
        member x.PushTag tag            = ()
        member x.PopTag ()              = ()
        member x.PushLabelWidth width   = ()
        member x.PopLabelWidth ()       = ()
        member x.LabelWidth             = 100.

type FormletControl<'TValue> (scrollViewer : ScrollViewer, submit : 'TValue -> unit, formlet : Formlet<FormletContext, UIElement, 'TValue>) as this =
    inherit DecoratorElement (scrollViewer)

    let queue                       = SingleDispatchQueue<FormletDispatchAction> (this.Dispatcher)
    let mutable formTree            = Empty
    let mutable changeGeneration    = 0

    do
        AddRoutedEventHandler FormletElement.RebuildEvent  this this.OnRebuild
        AddRoutedEventHandler FormletElement.SubmitEvent   this this.OnSubmit
        AddRoutedEventHandler FormletElement.ResetEvent    this this.OnReset

        scrollViewer.HorizontalScrollBarVisibility  <- ScrollBarVisibility.Disabled
        scrollViewer.VerticalScrollBarVisibility    <- ScrollBarVisibility.Visible

    let layout = FormletLayout.New TopToBottom Maximize Maximize

    let setElement (collection : IList) (position : int) (element : UIElement) : unit =
        if position < collection.Count then
            collection.[position] <- element
        else if position = collection.Count then
            ignore <| collection.Add element
        else
            HardFail_InvalidCase ()

    let getElement (collection : IList) (position : int) : UIElement =
        if position < collection.Count then
            match collection.[position] with
            | :? UIElement as e -> e
            | _                 -> null
        else null

    let trimElements (collection : IList) (count : int) = 
        let c = collection.Count
        for i in (c - 1)..(-1)..count do
            collection.RemoveAt i

    let rec buildTree (collection : IList) (position : int) (fl : FormletLayout) (ft : FormletTree<UIElement>) : int =
        let current = getElement collection position

        // TODO: Layout should be set
        match ft with
        | Empty                 ->
            0
        | Element e           ->
            setElement collection position e
            1
        | Adorner (e, ls, fts) ->
            let c = fts |> List.mapi (fun i v -> buildTree ls i fl v) |> List.sum
            trimElements ls c
            setElement collection position e
            1
        | Layout (l, ft)        ->
            let nl = fl.Union l
            if nl = fl then
                buildTree collection position fl ft
            else
                let sp = CreateElement current CreateVerticalStackPanel
                sp.Orientation <-
                    match fl.Orientation with
                    | FormletOrientation.Parent
                    | FormletOrientation.TopToBottom   -> Orientation.Vertical
                    | FormletOrientation.LeftToRight   -> Orientation.Horizontal
                
                let ls  = sp.Children
                let c   = buildTree ls 0 fl ft
                trimElements ls c
                setElement collection position sp
                1
        | Label (lbl, ft)       ->
            let label = CreateElement current (fun () -> LabelElement (100.))
            label.Text  <- lbl
            let ls  = label.ChildCollection
            let c   = buildTree ls 0 fl ft
            trimElements ls c
            setElement collection position label
            1
        | Fork (l,r)            ->
            let lcount = buildTree collection position fl l
            let rcount = buildTree collection (position + lcount) fl r
            lcount + rcount
        | Modify (modifier, ft)     ->
            let c       = buildTree collection position fl ft
            let element = getElement collection position
            modifier element
            c   // TODO: Should map be applied to last, first or all?
        | Group (_, ft)         ->
            buildTree collection position fl ft
        | Tag (_, ft)           ->
            buildTree collection position fl ft
        | Cache (_, ft)         ->
            buildTree collection position fl ft

    new (submit : 'TValue -> unit, formlet : Formlet<FormletContext, UIElement, 'TValue>) = 
        let scrollViewer = new ScrollViewer ()
        FormletControl (scrollViewer, submit, formlet)

    member this.OnRebuild   (sender : obj) (e : RoutedEventArgs) = queue.Dispatch (FormletDispatchAction.Rebuild  , this.BuildForm)
    member this.OnSubmit    (sender : obj) (e : RoutedEventArgs) = queue.Dispatch (FormletDispatchAction.Submit   , this.SubmitForm)
    member this.OnReset     (sender : obj) (e : RoutedEventArgs) = queue.Dispatch (FormletDispatchAction.Reset    , this.ResetForm)

    override this.OnStartUp () =
        this.BuildForm ()

    member this.ResetForm () =
        scrollViewer.Content <- null
        this.BuildForm ()

    member this.Evaluate () =
        let context = FormletContext ()
        let c,ft = formlet.Evaluate (context, [], formTree)
        // TODO: "Dispose" visual elements that are no longer in tree
        formTree <- ft 
        c,ft

    member this.SubmitForm () =
        let c,_ = this.Evaluate ()

        if not c.HasFailures then
            submit c.Value

    member this.BuildForm () =
        let _,ft= this.Evaluate ()
        let cft = FormletTree.Layout (layout, ft)
        let sp  = CreateElement scrollViewer.Content CreateVerticalStackPanel

        // TODO: Defer updates

        let ls  = sp.Children
        let c   = buildTree ls 0 layout cft
        trimElements ls c

        scrollViewer.Content <- sp

        ()
