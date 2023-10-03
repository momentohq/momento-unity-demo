# momento-unity-demo

Adds a minimally working MVP demo of Momento's Topics inside Unity 2022.3.9f1.

https://github.com/momentohq/momento-unity-demo/assets/276325/91f9d9d2-760e-413f-abb6-d0d3ed3cbfac

How to test:
1. Clone this repository.
2. Download the MomentoSdkUnity.zip (TODO add link) and extract it into Assets/MomentoSdkUnity
3. Ensure the `MOMENTO_AUTH_TOKEN` environment variable is set
4. Install Unity 2022.3.9f1
5. Open the project
6. Hit the play button to run inside the Unity Editor
7. To duplicate the screen recording, build the project via File --> Build Settings...
   1. Add Open Scenes (if necessary)
   2. Build
   3. Open the executable twice

Notes:
- Tested with two Windows builds of the Unity project + subscribing/publishing to the Topic in the Momento Console website
- There are two example scripts, `TopicsTest.cs` and `TopicsTestCoroutine.cs`, where the former utilizes `Task.Run()` to run the subscription to the Momento Topic in a background thread, while the latter utilizes Unity Coroutines to run the subscription asyncronously in the main thread.