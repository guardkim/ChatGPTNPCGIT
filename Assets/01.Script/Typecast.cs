using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


[System.Serializable]
public class SpeakRequestData
{
    public string text;
    public string lang;
    public string actor_id;
    public bool xapi_hd;
    public string model_version;
}


public class Typecast : MonoBehaviour
{
    private const string VOICE_ID = "622964d6255364be41659078";

    public Task<AudioClip> StartSpeechAsync(string text)
    {
        var tcs = new TaskCompletionSource<AudioClip>();
        StartCoroutine(RequestSpeakCoroutine(text, tcs));
        return tcs.Task;
    }

    private IEnumerator RequestSpeakCoroutine(string text, TaskCompletionSource<AudioClip> tcs)
    {
        // Step 1: POST /api/speak
        var speakData = new SpeakRequestData
        {
            text = text,
            lang = "auto",
            actor_id = VOICE_ID,
            xapi_hd = true,
            model_version = "latest"
        };

        string postJson = JsonUtility.ToJson(speakData);

        using (var postReq = new UnityWebRequest("https://typecast.ai/api/speak", "POST"))
        {
            postReq.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(postJson));
            postReq.downloadHandler = new DownloadHandlerBuffer();
            postReq.SetRequestHeader("Content-Type", "application/json");
            postReq.SetRequestHeader("Authorization", $"Bearer {APIKeys.TYPECAST_API_KEY}");

            Debug.Log(postJson);
            
            yield return postReq.SendWebRequest();

            if (postReq.result != UnityWebRequest.Result.Success)
            {
                tcs.SetException(new Exception($"Speak 요청 실패: {postReq.error}"));
                yield break;
            }

            string speakUrl = JsonUtility.FromJson<SpeakInitResp>(postReq.downloadHandler.text).result.speak_v2_url;

            // Step 2: Polling
            string audioUrl = null;
            while (audioUrl == null)
            {
                yield return new WaitForSeconds(1f);

                using (var getReq = UnityWebRequest.Get(speakUrl))
                {
                    getReq.SetRequestHeader("Authorization", $"Bearer {APIKeys.TYPECAST_API_KEY}");
                    yield return getReq.SendWebRequest();

                    if (getReq.result != UnityWebRequest.Result.Success)
                    {
                        tcs.SetException(new Exception($"상태 조회 실패: {getReq.error}"));
                        yield break;
                    }

                    var resp = JsonUtility.FromJson<SpeakStatusResp>(getReq.downloadHandler.text);
                    if (resp.result.status == "done")
                    {
                        audioUrl = resp.result.audio_download_url;
                    }
                }
            }

            // Step 3: Download audio
            using (var audioReq = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
            {
                yield return audioReq.SendWebRequest();

                if (audioReq.result != UnityWebRequest.Result.Success)
                {
                    tcs.SetException(new Exception($"오디오 다운로드 실패: {audioReq.error}"));
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(audioReq);
                tcs.SetResult(clip);
            }
        }
    }

    [Serializable] class SpeakInitResp { public InitResult result; }
    [Serializable] class InitResult { public string speak_v2_url; }
    [Serializable] class SpeakStatusResp { public StatusResult result; }
    [Serializable] class StatusResult { public string status; public string audio_download_url; }
}
