using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    //public TextMeshProUGUI ResultTextUI;
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
    public ScrollRect ScrollRect;
    public GameObject AIChatPanel;
    public GameObject PlayerChatPanel;
    public GameObject ChatPanelParent;
    [SerializeField] private Typecast _typecast;
    
    private void Start()
    {
        // API 클라이언트 초기화 : 1. ChatGPT 접속
        _openAIClient = new OpenAIClient(APIKeys.OPENAI_API_KEY);

        CreateImage();
        
        // JSON 응답을 요구하는 시스템 메시지 수정
        string systemMessage = Settings.Description; 
            // "\n\nIMPORTANT: You must ALWAYS respond in valid JSON format with exactly this structure:\n" +
            // "{\n" +
            // "  \"ReplyMessage\": \"your main response message here\",\n" +
            // "  \"Appearance\": \"character appearance description\",\n" +
            // "  \"Emotion\": \"current emotion like happy, sad, angry, surprised, etc\",\n" +
            // "  \"StoryImageDescription\": \"detailed image description for generation\"\n" +
            // "}\n" +
            // "CRITICAL: Always include the ReplyMessage field with your actual response content. Do not include any text outside of this JSON structure.";
            
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
        
        Instantiate(PlayerChatPanel, ChatPanelParent.transform).GetComponent<UI_ChatPanel>().SetText(userMessage);
        ScrollToBottom();
        // 3. ChatRequest 생성시 response_format 지정 (GPT-4o의 경우)
        var chatRequest = new ChatRequest(_conversationHistory, Model.GPT4o);
        
        try
        {
            // 4. 답변 받기
            var response = await _openAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            
            // 5. 답변 선택
            var choice = response.FirstChoice;
            string responseContent = choice.Message.Content.ToString();
            
            Debug.Log($"📨 원본 응답: {responseContent}");
            
            // AI 응답을 대화 기록에 추가 (원본 메시지로 복원)
            var aiMessage = new Message(Role.Assistant, responseContent);
            _conversationHistory.Add(aiMessage);
            // JSON 파싱 시도
            NPCResponse npcResponse = ParseNPCResponse(responseContent);
            
            // UI 업데이트
            Instantiate(AIChatPanel, ChatPanelParent.transform).GetComponent<UI_ChatPanel>().SetText(npcResponse.ReplyMessage);
            PlayTTS(npcResponse.ReplyMessage);
            GenerateImageFromNPCResponse(npcResponse);
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 메시지 전송 중 오류: {e.Message}");
            
            // 오류 시 기본 응답 생성
            var fallbackResponse = new NPCResponse
            {
                ReplyMessage = "죄송합니다. 응답을 처리하는 중 오류가 발생했습니다.",
                Appearance = "cute female soldier",
                Emotion = "apologetic",
                StoryImageDescription = "cute anime female soldier character with apologetic expression"
            };
            
            // ResultTextUI.text = fallbackResponse.ReplyMessage;
            Instantiate(AIChatPanel, ChatPanelParent.transform).GetComponent<UI_ChatPanel>().SetText(fallbackResponse.ReplyMessage);
            GenerateImageFromNPCResponse(fallbackResponse);
        }
            ScrollToBottom();
        
    }
    
    private NPCResponse ParseNPCResponse(string responseContent)
    {
        try
        {
            // 1. 먼저 JSON 블록을 찾아보기 (```json으로 감싸진 경우)
            string jsonContent = ExtractJsonFromResponse(responseContent);
            
            // 2. JSON 파싱 시도
            if (!string.IsNullOrEmpty(jsonContent))
            {
                Debug.Log($"🔍 파싱할 JSON: {jsonContent}");
                
                // 유연한 JSON 파싱 - 다양한 필드명 시도
                var npcResponse = ParseFlexibleJson(jsonContent);
                
                // 필수 필드 검증 및 보완
                if (string.IsNullOrEmpty(npcResponse.ReplyMessage))
                {
                    Debug.LogWarning("ReplyMessage가 비어있어서 원본 응답을 사용합니다.");
                    npcResponse.ReplyMessage = responseContent;
                }
                
                // 기본값 설정
                if (string.IsNullOrEmpty(npcResponse.Appearance))
                    npcResponse.Appearance = "cute female soldier";
                if (string.IsNullOrEmpty(npcResponse.Emotion))
                    npcResponse.Emotion = "neutral";
                if (string.IsNullOrEmpty(npcResponse.StoryImageDescription))
                    npcResponse.StoryImageDescription = $"{npcResponse.Appearance}, {npcResponse.Emotion} expression";
                
                Debug.Log($"✅ JSON 파싱 성공 - Reply: {npcResponse.ReplyMessage?.Substring(0, Math.Min(50, npcResponse.ReplyMessage.Length))}...");
                return npcResponse;
            }
            else
            {
                throw new Exception("유효한 JSON을 찾을 수 없습니다.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ JSON 파싱 실패: {e.Message}");
            Debug.LogWarning($"원본 응답: {responseContent}");
            
            // 파싱 실패 시 텍스트 분석으로 대체
            return CreateFallbackResponse(responseContent);
        }
    }
    
    private NPCResponse ParseFlexibleJson(string jsonContent)
    {
        try
        {
            // 먼저 직접 파싱 시도
            var npcResponse = JsonConvert.DeserializeObject<NPCResponse>(jsonContent);
            if (npcResponse != null && !string.IsNullOrEmpty(npcResponse.ReplyMessage))
            {
                return npcResponse;
            }
        }
        catch (Exception e)
        {
            Debug.Log($"직접 파싱 실패, 유연한 파싱 시도: {e.Message}");
        }
        
        // 유연한 파싱 - JObject 사용
        try
        {
            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);
            var result = new NPCResponse();
            
            // 다양한 필드명 시도
            result.ReplyMessage = GetValueFromJson(jsonObject, "ReplyMessage", "reply", "message", "response", "text", "content");
            result.Appearance = GetValueFromJson(jsonObject, "Appearance", "appearance", "look", "visual", "character");
            result.Emotion = GetValueFromJson(jsonObject, "Emotion", "emotion", "feeling", "mood", "state");
            result.StoryImageDescription = GetValueFromJson(jsonObject, "StoryImageDescription", "imageDescription", "image", "scene", "visual_description");
            
            Debug.Log($"🔧 유연한 파싱 결과 - Reply: '{result.ReplyMessage}', Emotion: '{result.Emotion}'");
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"유연한 JSON 파싱도 실패: {e.Message}");
            throw;
        }
    }
    
    private string GetValueFromJson(Dictionary<string, object> jsonObject, params string[] possibleKeys)
    {
        foreach (string key in possibleKeys)
        {
            // 대소문자 구분하지 않고 검색
            var actualKey = jsonObject.Keys.FirstOrDefault(k => 
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                
            if (actualKey != null && jsonObject[actualKey] != null)
            {
                return jsonObject[actualKey].ToString();
            }
        }
        return "";
    }
    
    private string ExtractJsonFromResponse(string response)
    {
        try
        {
            // Case 1: ```json 블록으로 감싸진 경우
            if (response.Contains("```json"))
            {
                int startIndex = response.IndexOf("```json") + 7;
                int endIndex = response.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    return response.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            // Case 2: { }로 감싸진 JSON 찾기
            int jsonStart = response.IndexOf('{');
            int jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string potentialJson = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // 간단한 JSON 유효성 검사
                if (potentialJson.Contains("\"ReplyMessage\"") || potentialJson.Contains("ReplyMessage"))
                {
                    return potentialJson;
                }
            }
            
            // Case 3: 전체 응답이 JSON인지 확인
            if (response.Trim().StartsWith("{") && response.Trim().EndsWith("}"))
            {
                return response.Trim();
            }
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 추출 중 오류: {e.Message}");
            return null;
        }
    }
    
    private NPCResponse CreateFallbackResponse(string originalResponse)
    {
        // 기본 키워드 분석으로 감정 추출 시도
        string emotion = "neutral";
        string appearance = "cute female soldier";
        
        // 감정 키워드 분석
        string lowerResponse = originalResponse.ToLower();
        if (lowerResponse.Contains("happy") || lowerResponse.Contains("smile") || lowerResponse.Contains("joy"))
            emotion = "happy";
        else if (lowerResponse.Contains("sad") || lowerResponse.Contains("cry"))
            emotion = "sad";
        else if (lowerResponse.Contains("angry") || lowerResponse.Contains("mad"))
            emotion = "angry";
        else if (lowerResponse.Contains("surprise") || lowerResponse.Contains("shock"))
            emotion = "surprised";
        else if (lowerResponse.Contains("confus") || lowerResponse.Contains("puzzle"))
            emotion = "confused";
        
        return new NPCResponse
        {
            ReplyMessage = originalResponse,
            Appearance = appearance,
            Emotion = emotion,
            StoryImageDescription = $"{appearance}, {emotion} expression, anime style"
        };
    }
    private async void PlayTTS(string text)
    {
        try
        {
            var request = new SpeechRequest(text);
            // var speechClip = await _openAIClient.AudioEndpoint.GetSpeechAsync(request);
            Task<AudioClip> speechClip = _typecast.StartSpeechAsync(request.);
            
            MyAudioSource.PlayOneShot(await speechClip);
        }
        catch (Exception e)
        {
            Debug.LogError($"TTS 오류: {e.Message}");
        }
    }

    private async void GenerateImageFromNPCResponse(NPCResponse npcResponse)
    {
        try
        {
            // NPC 응답에서 프롬프트 구성 요소 추출
            string appearance = npcResponse.Appearance ?? "cute female soldier";
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
                imagePrompt = "cute female soldier, happy";
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
    public void ScrollToBottom()
    {
        StartCoroutine(ScrollToBottomCoroutine());
    }
    private IEnumerator ScrollToBottomCoroutine()
    {
        if (ScrollRect == null) yield break;
        yield return new WaitForEndOfFrame();
        ScrollRect.verticalNormalizedPosition = 0f;
    }
    private void LateUpdate()
    {
        ScrollRect.verticalNormalizedPosition = 0f;

    }
}
