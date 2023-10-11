# momento-unity-demo

Adds a minimally working chat demo of Momento's Topics using Unity 2022.3.9f1.

https://github.com/momentohq/momento-unity-demo/assets/276325/91f9d9d2-760e-413f-abb6-d0d3ed3cbfac

# Setup
1. Install Unity 2022.3.9f1
2. Clone this repository
3. Download the latest MomentoSdkUnity-x.y.z.zip from https://github.com/momentohq/client-sdk-dotnet/releases and extract its contents into the folder `Assets/MomentoSdkUnity/`
4. Ensure the `MOMENTO_AUTH_TOKEN` environment variable is set, or copy and paste your auth token into `Assets/TopicsTest.cs` (or `Assets/TopicsTestCoroutine.cs`) replacing `ADD_YOUR_TOKEN_HERE` in the `ReadAuthToken()` function (hard-coding your auth token in code is not recommended but can be used for testing purposes if necessary)
5. Start Unity and open this folder
6. Open the `MomentTopicsDemo.unity` scene file
7. Hit the play button to run inside the Unity Editor
8. To duplicate the screen recording above, build the project via File --> Build Settings...
   1. Add Open Scenes (if necessary)
   2. Build (make a new folder called `Build` to build into)
   3. If needed, add a firewall rule to allow your executable to access your network 
   4. Open the executable twice

# Notes
- Tested with two Windows builds of the Unity project + subscribing/publishing to the Topic in the Momento Console website
- There are two example scripts, `TopicsTest.cs` and `TopicsTestCoroutine.cs`, where the former utilizes `Task.Run()` to run the subscription to the Momento Topic in a background thread, while the latter utilizes Unity Coroutines to run the subscription asyncronously in the main thread. If you want to use `TopicsTestCoroutine.cs`, make sure to enable that script, disable `TopicsTest.cs` and finally update the "On Click" callbacks for the "Start" and "Send" buttons.
- WebGL builds currently do not work.