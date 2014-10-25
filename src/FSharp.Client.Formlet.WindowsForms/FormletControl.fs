namespace FSharp.Client.Formlet.WindowsForms


open System.Windows.Forms

open FSharp.Client.Formlet.Core

type FormletContext () =
    interface IFormletContext with
        member this.PushTag tag = ()
        member this.PopTag ()   = ()

type FormletControl<'TValue> (submit : 'TValue -> unit, formlet : Formlet<FormletContext, Control, 'TValue>) as this =
    inherit Control()
    let x = 3


