# momento-unity-demo

Several chat demos of Momento's Topics using Unity 2022.3.9f1.

# Quick Setup
1. Install Unity 2022.3.9f1
2. Clone this repository
3. Download the latest MomentoSdkUnity-x.y.z.zip from https://github.com/momentohq/client-sdk-dotnet/releases and extract its contents into the folder `Assets/MomentoSdkUnity/`. This example demo requires [client-dotnet-sdk release v1.30.1](https://github.com/momentohq/client-sdk-dotnet/releases/tag/v1.30.1), or newer.
4. Start Unity and open this folder
5. Open the `Assets/Scenes/MomentoTopicsDemo-ModeratedChat.unity` scene file
6. Hit the play button to run inside the Unity Editor

If needed, add a firewall rule to allow your Unity or your built executable to access your network.

# Notes
- Tested with two Windows builds of the Unity project + subscribing/publishing to the Topic in the Momento Console website
- There are four example scenes which run different example scripts. The first two require ensuring the `MOMENTO_AUTH_TOKEN` environment variable is set, or copying and pasting your auth token into `Assets/Scripts/TopicsTest.cs` (or `Assets/Scripts/TopicsTestCoroutine.cs`) replacing `ADD_YOUR_TOKEN_HERE` in the `ReadAuthToken()` function (hard-coding your auth token in code is not recommended but can be used for testing purposes if necessary).
   - `MomentoTopicsDemo.unity` (using `TopicsTest.cs`): utilizes `Task.Run()` to run the subscription to the Momento Topic in a background thread
   - `MomentoTopicsDemo-Coroutine.unity` (using `TopicsTestCoroutine.cs`): utilizes Unity Coroutines to run the subscription asyncronously in the main thread.
   - `MomentoTopicsDemo-TokenVendingMachine.unity` (using `TopicsTestTokenVendingMachine.cs`): utilizes the example [Momento Token Vending Machine](https://github.com/momentohq/client-sdk-javascript/tree/main/examples/nodejs/token-vending-machine) to obtain a temporary, restricted scope Momento auth token. This is beneficial because (1) we no longer need to hard-code in a specific auth token into the app, and (2) we can utilize a `tokenId` embedded in the Topics message to more securely know which client/username sent a specific message. Note that you'll need to explicitly setup the Token Vending Machine separately and then specify its URL via the `tokenVendingMachineURL` variable specified in `TopicsTestTokenVendingMachine.cs`.
   - `MomentoTopicsDemo-ModeratedChat.unity`: corresponding Unity client for the Momento moderated chat demo (see https://github.com/momentohq/moderated-chat/tree/main and https://chat.gomomento.com/). As such, much of the code follows from the [frontend web client](https://github.com/momentohq/moderated-chat/tree/main/frontend) that already exists.
- WebGL builds currently do not work.
