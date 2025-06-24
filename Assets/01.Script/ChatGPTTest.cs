using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json;

public class ChatGPTTest : MonoBehaviour
{
    public TextMeshProUGUI ResultTextUI;
    public TMP_InputField PromptField;
    public Button SendButton;
    public RawImage image;
    public AudioSource MyAudioSource;
    public NPCSetting Settings;
    private string _sendingMessage;
    private OpenAIClient _openAIClient;

    public ComfyUIClient Client;
    // 대화 기록을 Message 타입으로 저장
    private List<Message> _conversationHistory = new List<Message>();

    private string _positivePrompt = "";
    
    private void Start()
    {
        // API 클라이언트 초기화 : 1. ChatGPT 접속
        _openAIClient = new OpenAIClient(APIKeys.OPENAI_API_KEY);

        CreateImage();
        // CHAT-F
        // C  : Context         : 문맥, 상황을 많이 알려줘라
        // H  : Hint            : 예시 답변을 많이 줘라
        // A  : As A role       : 역할을 제공해라
        // T  : Target          : 답변의 타겟을 알려줘라
        // F  : Format          : 답변 형태를 지정해라
        string systemMessage = Settings.Description;
        _conversationHistory.Add(new Message(Role.System, systemMessage));
    }
    public async void OnClickSendButton()
    {
        if (string.IsNullOrEmpty(PromptField.text)) return;
        if (_openAIClient == null) return;
        SendMessage(PromptField.text);
    }

    public void OnClickCreateImage()
    {
        CreateImage();
    }
    public void CreateImage()
    {
        StartCoroutine(Client.GenerateImageAndWait(_positivePrompt, (imagePath) =>
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                Debug.Log("Generated image path: " + imagePath);
                StartCoroutine(LoadAndShowImageAlternative(imagePath));
            }
            else
            {
                Debug.LogError("Failed to get image path");
            }
        }));
    }
    public async void SendMessage(string message)
    {
        PromptField.text = string.Empty;
        
        // 사용자 메시지를 대화 기록에 추가
        var userMessage = new Message(Role.User, message);
        _conversationHistory.Add(userMessage);
        
        // 2. 전체 대화 기록을 포함한 메시지 리스트 생성
        var messages = new List<Message>(_conversationHistory);
        
        // 3. 대안: JsonSchema 없이 일반적인 ChatRequest 사용
        var chatRequest = new ChatRequest(messages, Model.GPT4o);
        
        // 4. 답변 받기 (일반적인 방식)
        var response = await _openAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
        
        // 5. 답변 선택 및 JSON 파싱
        var choice = response.FirstChoice;
        // AI 응답을 대화 기록에 추가
        var aiMessage = new Message(Role.Assistant, choice.Message.Content.ToString());
        _conversationHistory.Add(aiMessage);
        
        // JSON 문자열을 NPCResponse 객체로 파싱
        NPCResponse npcResponse = null;
        string responseContent = choice.Message.Content.ToString();
        
        try
        {
            // 응답 내용이 JSON인지 확인
            if (responseContent.Trim().StartsWith("{") && responseContent.Trim().EndsWith("}"))
            {
                npcResponse = JsonConvert.DeserializeObject<NPCResponse>(responseContent);
                Debug.Log($"✅ JSON 파싱 성공: {responseContent}");
            }
            else
            {
                Debug.LogWarning($"⚠️ JSON 형식이 아닌 응답: {responseContent}");
                throw new Exception("응답이 JSON 형식이 아닙니다.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message}");
            Debug.LogError($"원본 응답: {responseContent}");
            
            // 파싱 실패 시 기본값 사용
            npcResponse = new NPCResponse
            {
                ReplyMessage = responseContent,
                Appearance = "cute cat girl with fluffy ears",
                Emotion = "curious",
                StoryImageDescription = "cute anime cat girl character with fluffy ears, curious expression"
            };
        }
        
        
        // UI 업데이트
        ResultTextUI.text = npcResponse.ReplyMessage;
        PlayTTS(npcResponse.ReplyMessage);
        GenerateImageFromNPCResponse(npcResponse);

    }
    private async void PlayTTS(string text)
    {
        var request = new SpeechRequest(text);
        var speechClip = await _openAIClient.AudioEndpoint.GetSpeechAsync(request);
        MyAudioSource.PlayOneShot(speechClip);
    }

    private async void GenerateImageFromNPCResponse(NPCResponse npcResponse)
    {
        try
        {
            // NPC 응답에서 프롬프트 구성 요소 추출
            string appearance = npcResponse.Appearance ?? "cute cat girl";
            string emotion = npcResponse.Emotion ?? "happy";
            string storyDescription = npcResponse.StoryImageDescription ?? "";
            
            // 이미지 생성용 프롬프트 조합
            string imagePrompt = "";
            
            // 외모 정보 추가
            if (!string.IsNullOrEmpty(appearance))
            {
                imagePrompt += appearance + ", ";
            }
            
            // 감정 정보 추가
            if (!string.IsNullOrEmpty(emotion))
            {
                imagePrompt += emotion + " expression, ";
            }
            
            // 전체 이미지 설명 추가
            if (!string.IsNullOrEmpty(storyDescription))
            {
                imagePrompt += storyDescription;
            }
            
            // 빈 프롬프트 방지
            if (string.IsNullOrEmpty(imagePrompt.Trim()))
            {
                imagePrompt = "cute cat girl, happy";
            }
            
            // 프롬프트 저장 및 로그
            _positivePrompt = imagePrompt.Trim().TrimEnd(',');
            Debug.Log($"🎨 이미지 생성 프롬프트: {_positivePrompt}");
            
            // 이미지 생성 시작
            StartCoroutine(Client.GenerateImageAndWait(_positivePrompt, (imagePath) =>
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    Debug.Log($"✅ 이미지 생성 완료: {imagePath}");
                    StartCoroutine(LoadAndShowImageAlternative(imagePath));
                }
                else
                {
                    Debug.LogError("❌ 이미지 생성 실패");
                }
            }));
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 이미지 프롬프트 생성 중 오류: {e.Message}");
        }
    }
    public IEnumerator LoadAndShowImageAlternative(string imagePath)
    {
        if (File.Exists(imagePath))
        {
            byte[] fileData = File.ReadAllBytes(imagePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            image.texture = tex;
        }
        else
        {
            Debug.LogError("File not found: " + imagePath);
        }
    
        yield return null;
    }
}