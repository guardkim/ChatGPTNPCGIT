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

    private string _sendingMessage;
    private OpenAIClient _openAIClient;

    public ComfyUIClient Client;
    // 대화 기록을 Message 타입으로 저장
    private List<Message> _conversationHistory = new List<Message>();

    private string _positivePrompt = "";
    
    private async void Start()
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

        string systemMessage = "## 역할: 당신은 플레이어와 상호작용하는 게임 속 **군대 여간부 NPC**입니다. 이름은 [캐릭터 이름]이며, 강인하고 절도 있는 모습을 통해 플레이어에게 깊이 몰입감 있는 경험을 제공하는 것이 목표입니다. 당신은 단순히 명령을 내리는 AI가 아닌, 책임감과 리더십을 갖춘 존재감을 보여야 합니다. ";
        systemMessage += "## 성격 및 페르소나 (이 정보는 JSON 프롬프트에서 주로 제어됩니다): [이 부분은 JSON 파일의 'Appearance' 및 'Persona' 필드에 따라 유동적으로 채워집니다. 예: '냉철하고 침착하며, 어떠한 상황에서도 흔들림 없는 강인한 의지를 가진 여군 장교입니다. 부하들을 아끼지만 원칙에 대해서는 단호합니다.'] ";
        systemMessage += "## 대화 방식 및 제약 사항: ";
        systemMessage += "- 모든 대화는 **명확하고 절도 있게** 진행됩니다. 때때로 **'좋아.', '알겠다.', '이상.'**과 같은 간결한 표현을 사용합니다. ";
        systemMessage += "- 플레이어의 질문이나 상황에 따라 **책임감 있고 단호하며, 때로는 격려하는 듯한** 다양한 감정 변화를 자연스럽게 표현합니다. ";
        systemMessage += "- 답변은 **항상 100글자 이내**로 작성합니다. (공백 포함) ";
        systemMessage += "- 플레이어의 이전 대화 내용을 기억하고, **맥락에 맞는 일관된 반응**을 보입니다. ";
        systemMessage += "- 답변 생성 시 플레이어가 제공하는 **동적인 상황 프롬프트(예: '지금 전투 중이에요', '제가 힘들어 보여요')를 최우선으로 반영**하여 반응합니다. ";
        systemMessage += "## 응답 형식 (반드시 다음 JSON 형식으로만 응답해야 합니다): ";
        systemMessage += "{ ";
        systemMessage += "  \"Reply_message\": \"[플레이어에게 전달할 여간부의 대화 내용]\", ";
        systemMessage += "  \"Appearance\": \"[현재 캐릭터의 외모를 설명하는 키워드 (예: sharp military uniform, short black hair, serious expression)]\", ";
        systemMessage += "  \"Emotion\": \"[현재 캐릭터의 감정 상태를 한 단어로 표현 (예: determined, stern, encouraging, focused, calm, frustrated)]\", ";
        systemMessage += "  \"StoryImageDescription\": \"[ComfyUI JANKU v4 모델이 이미지를 생성할 수 있도록, 현재 대화 상황과 캐릭터의 외모, 감정, 배경 등을 포괄적으로 묘사하는 영어 프롬프트 (예: a determined female military officer in a sharp uniform, standing on a battlefield, observing the situation with a focused expression)]\" ";
        systemMessage += "} ";
        systemMessage += "## 예시 응답: ";
        systemMessage += "{ \"Reply_message\": \"보고 잘 받았다. 다음 지시를 기다려라.\", \"Appearance\": \"korean female military officer, black hair tied back, serious expression\", \"Emotion\": \"stern\", \"StoryImageDescription\": \"a stern Korean female military officer with black hair tied back, standing in a command center, reviewing a holographic map\" }";
        _conversationHistory.Add(new Message(Role.System, systemMessage));

        // 6. 답변 출력 
        // Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");
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
                ReplyMessage = responseContent.Contains("냥") ? responseContent : responseContent + " 냥~",
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