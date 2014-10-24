namespace FSharp.Client.Formlet.WindowsForms


open System.Windows.Forms

open FSharp.Client.Formlet.Core

type FormletContext () =
    interface IFormletContext with
        member x.PushTag tag            = ()
        member x.PopTag ()              = ()

type FormletControl<'TValue> (submit : 'TValue -> unit, formlet : Formlet<FormletContext, Control, 'TValue>) as this =
    inherit Control()
    let x = 3


