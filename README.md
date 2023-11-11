# momento-unity-demo

Adds a minimally working chat demo of Momento's Topics using Unity 2022.3.9f1.

# Setup
1. Install Unity 2022.3.9f1
2. Clone this repository
3. Download the latest MomentoSdkUnity-x.y.z.zip from https://github.com/momentohq/client-sdk-dotnet/releases and extract its contents into the folder `Assets/MomentoSdkUnity/`. This example demo requires [client-dotnet-sdk release v1.25.0](https://github.com/momentohq/client-sdk-dotnet/releases/tag/v1.25.0), or newer.
4. Ensure the `MOMENTO_AUTH_TOKEN` environment variable is set, or copy and paste your auth token into `Assets/TopicsTest.cs` (or `Assets/TopicsTestCoroutine.cs`) replacing `ADD_YOUR_TOKEN_HERE` in the `ReadAuthToken()` function (hard-coding your auth token in code is not recommended but can be used for testing purposes if necessary)
5. Start Unity and open this folder
6. Open the `MomentTopicsDemo.unity` scene file
7. Hit the play button to run inside the Unity Editor
8. To duplicate the screen recording above, build the project via File --> Build Settings...
   1. Add Open Scenes (if necessary)
   2. Select which scene you want to build (optional)
   3. Build (make a new folder called `Build` to build into)
   4. If needed, add a firewall rule to allow your executable to access your network 
   5. Open the executable twice

# Notes
- Tested with two Windows builds of the Unity project + subscribing/publishing to the Topic in the Momento Console website
- There are three example scenes which run different example scripts:
   - `MomentoTopicsDemo.unity` (using `TopicsTest.cs`): utilizes `Task.Run()` to run the subscription to the Momento Topic in a background thread
   - `MomentoTopicsDemo-Coroutine.unity` (using `TopicsTestCoroutine.cs`): utilizes Unity Coroutines to run the subscription asyncronously in the main thread.
   - `MomentoTopicsDemo-TokenVendingMachine.unity` (using `TopicsTestTokenVendingMachine.cs`): utilizes the example [Momento Token Vending Machine](https://github.com/momentohq/client-sdk-javascript/tree/main/examples/nodejs/token-vending-machine) to obtain a temporary, restricted scope Momento auth token. This is beneficial because (1) we no longer need to hard-code in a specific auth token into the app, and (2) we can utilize a `tokenId` embedded in the Topics message to more securely know which client/username sent a specific message. Note that you'll need to explicitly setup the Token Vending Machine separately and then specify its URL in `TopicsTestTokenVendingMachine.cs` for this to work.
- WebGL builds currently do not work.
