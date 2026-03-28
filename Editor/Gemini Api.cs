using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

public enum PersonaMode
{
    Standart,
    YazilimGelistirici,
    OyunTasarimcisi,
    ShaderUzmani
}

public class GeminiWindow : EditorWindow
{
    private string apiKey = "TOKENİ BURAYA GİRCEN KUZEN AQQ"; 
    private string modelName = "gemini-2.5-flash"; 
    private string projectGDD = ""; // Global Context (Sürekli Proje Hafızası)

    private List<ChatTab> tabs = new List<ChatTab>();
    private int selectedTab = 0;
    
    private bool showSettings = false;
    [System.NonSerialized] private AudioClip recordingClip;
    [System.NonSerialized] private bool isRecording = false;

    private void OnEnable()
    {
        apiKey = EditorPrefs.GetString("Gemini_ApiKey", "AIzaSyDWi1jotyrdXGu0XqAbO7NDF29vBnZ-uvw");
        modelName = EditorPrefs.GetString("Gemini_ModelName", "gemini-2.5-flash");
        projectGDD = EditorPrefs.GetString("Gemini_ProjectGDD", "");

        if (tabs.Count == 0)
        {
            tabs.Add(new ChatTab { tabName = "Sohbet 1" });
        }
    }

    [MenuItem("Tools/Gemini AI Chat")]
    public static void ShowWindow() => GetWindow<GeminiWindow>("Gemini AI Chat (Master)");

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("GEMINI ASİSTAN (MASTER)", EditorStyles.boldLabel);
        if (GUILayout.Button("⚙️ Ayarlar", EditorStyles.miniButtonRight, GUILayout.Width(80))) showSettings = !showSettings;
        GUILayout.EndHorizontal();

        if (showSettings)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            apiKey = EditorGUILayout.TextField("API Anahtarı:", apiKey);
            modelName = EditorGUILayout.TextField("Model Adı:", modelName);
            
            GUILayout.Space(5);
            GUILayout.Label("Oyun Tasarım Dökümanı (GDD / Global Context)", EditorStyles.boldLabel);
            GUILayout.Label("Oyununuzun mekaniklerini ve hikayesini buraya bir kere yazın. Bütün sekmeler bu hafızayı hatırlar:", EditorStyles.wordWrappedLabel);
            projectGDD = EditorGUILayout.TextArea(projectGDD, GUILayout.Height(60));

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Kaydet", GUILayout.Width(70)))
            {
                EditorPrefs.SetString("Gemini_ApiKey", apiKey);
                EditorPrefs.SetString("Gemini_ModelName", modelName);
                EditorPrefs.SetString("Gemini_ProjectGDD", projectGDD);
                showSettings = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // Sekme (Tab) Arayüzü
        GUILayout.Space(5);
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        for (int i = 0; i < tabs.Count; i++)
        {
            if (GUILayout.Toggle(selectedTab == i, tabs[i].tabName, EditorStyles.toolbarButton))
            {
                selectedTab = i;
            }
        }
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            tabs.Add(new ChatTab { tabName = $"Sohbet {tabs.Count + 1}" });
            selectedTab = tabs.Count - 1;
        }
        if (tabs.Count > 1 && GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            tabs.RemoveAt(selectedTab);
            if (selectedTab >= tabs.Count) selectedTab = tabs.Count - 1;
        }
        GUILayout.EndHorizontal();

        ChatTab tab = tabs[selectedTab];

        // Sekme Ayarları ve Uzmanlık Alanı
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Sekme Adı:", GUILayout.Width(70));
        tab.tabName = EditorGUILayout.TextField(tab.tabName, GUILayout.Width(150));
        
        GUILayout.Space(20);
        
        GUILayout.Label("Uzmanlık:", GUILayout.Width(65));
        tab.mode = (PersonaMode)EditorGUILayout.EnumPopup(tab.mode, GUILayout.Width(150));
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        tab.useWebSearch = GUILayout.Toggle(tab.useWebSearch, " 🌍 Web Araması (İnterneti Kullanılarak Güncel Cevaplar Ver)");
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        
        // Hızlı Eylemler (Quick Actions)
        GUILayout.BeginHorizontal();
        GUILayout.Label("Hızlı Eylemler:", EditorStyles.boldLabel, GUILayout.Width(90));
        if (GUILayout.Button("🔍 Optimize Et", EditorStyles.miniButton))
        {
            tab.prompt += "\nLütfen bu kodu detaylıca incele, performans ve mimari açıdan optimize et ve düzeltilmiş en temiz halini C# bloğu içine alarak bana sun.";
        }
        if (GUILayout.Button("📑 Yorum Ekle", EditorStyles.miniButton))
        {
            tab.prompt += "\nLütfen bu kodun ne yaptığını açıklayan, karmaşık fonksiyonlara // yorum satırları (XML formatında summary dahil) ekle ve kodu bana geri ver.";
        }
        if (GUILayout.Button("🐛 Bug Ara", EditorStyles.miniButton))
        {
            tab.prompt += "\nBu kodda mantıksal çökme, potansiyel Memory Leak veya NullReference hataları var mı? İncele ve güvenli halini yaz.";
        }
        if (GUILayout.Button("🌐 Çeviri Yap", EditorStyles.miniButton))
        {
            tab.prompt += "\nLütfen ekte verdiğim metin/JSON dosyasını incele. Orijinal programlama formatına ASLA dokunmadan ve bozmadan, sadece oyun diyaloglarını/metinlerini yabancı dillere çevirip dosyayı geri ver.";
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        tab.prompt = EditorGUILayout.TextArea(tab.prompt, GUILayout.Height(100));

        // Yardımcı Butonlar (Console & Object Context & Hierarchy & Vision & Audio)
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Tüm Sahneyi Analiz Et", EditorStyles.miniButton))
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Sahnedeki Objelerin Hiyerarşik Dağılımı (Scene Graph):");
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                TraverseHierarchy(root, 0, sb);
            }

            tab.prompt = $"{sb.ToString()}\n\n{tab.prompt}";
        }
        
        if (GUILayout.Button("Seçili Objeleri Ekle", EditorStyles.miniButton))
        {
            if (Selection.gameObjects.Length > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Seçtiğim objeler ve component listesi:");
                foreach (GameObject go in Selection.gameObjects)
                {
                    sb.AppendLine($"- Obje: '{go.name}', Etiket(Tag): '{go.tag}', Katman(Layer): '{LayerMask.LayerToName(go.layer)}'");
                    Component[] components = go.GetComponents<Component>();
                    sb.Append("  Bileşenler(Components): ");
                    for (int j = 0; j < components.Length; j++)
                    {
                        if (components[j] != null) sb.Append(components[j].GetType().Name);
                        if (j < components.Length - 1) sb.Append(", ");
                    }
                    sb.AppendLine();
                }
                tab.prompt = $"{sb.ToString()}\n\n{tab.prompt}";
            }
        }
        
        if (GUILayout.Button("Son Hatayı Oku", EditorStyles.miniButton))
        {
            string error = GetLastConsoleError();
            if (!string.IsNullOrEmpty(error))
            {
                tab.prompt = $"Konsolda aşağıdaki hatayı alıyorum, sebebi nedir ve nasıl çözerim?\n\n[KONSOL HATASI]:\n{error}\n\n{tab.prompt}";
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("📷 Görüntü Ekle (Game View)", EditorStyles.miniButton))
        {
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                tab.pendingVisionBytes = tex.EncodeToJPG(75);
                DestroyImmediate(tex);
            }
            else
            {
                Debug.LogWarning("Oyun ekranı yakalanamadı. Lütfen Game sekmesinin arkaplanda dahi olsa görünür veya aktif olduğundan emin olun.");
            }
        }

        if (GUILayout.Button(isRecording ? "🔴 Kaydı Bitir" : "🎤 Kayıt Al", EditorStyles.miniButton))
        {
            if (isRecording)
            {
                Microphone.End(null);
                isRecording = false;
                if (recordingClip != null)
                {
                    tab.pendingAudioBytes = EncodeToWAV(recordingClip);
                    DestroyImmediate(recordingClip);
                }
            }
            else
            {
                recordingClip = Microphone.Start(null, false, 60, 16000);
                isRecording = true;
            }
        }
        GUILayout.EndHorizontal();

        // Görüntü ve Ses Eklenmişse Uyarıları Göster
        if (tab.pendingVisionBytes != null || tab.pendingAudioBytes != null)
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (tab.pendingVisionBytes != null)
            {
                GUILayout.Label($"📷 Görüntü ({tab.pendingVisionBytes.Length / 1024} KB)");
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25))) tab.pendingVisionBytes = null;
            }
            if (tab.pendingAudioBytes != null)
            {
                GUILayout.Label($"🎤 Ses Kaydı ({tab.pendingAudioBytes.Length / 1024} KB)");
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25))) tab.pendingAudioBytes = null;
            }
            GUILayout.EndHorizontal();
        }

        // ÇOKLU DOSYA SÜRÜKLE BIRAK SİSTEMİ
        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("📂 Dosya Ekleri (C# kodları, JSON, CSV vb.) Sürükle ve Bırak:", EditorStyles.boldLabel);
        
        GUIStyle dropZoneStyle = new GUIStyle(GUI.skin.box);
        dropZoneStyle.alignment = TextAnchor.MiddleCenter;
        dropZoneStyle.normal.textColor = Color.gray;
        GUILayout.Box("+ Yeni Dosyaları Buraya Sürükleyin +", dropZoneStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
        
        Event ev = Event.current;
        Rect dropRect = GUILayoutUtility.GetLastRect();
        if (dropRect.Contains(ev.mousePosition))
        {
            if (ev.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                ev.Use();
            }
            else if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject != null && !tab.attachedFiles.Contains(draggedObject))
                        tab.attachedFiles.Add(draggedObject);
                }
                ev.Use();
            }
        }

        if (tab.attachedFiles.Count > 0)
        {
            for (int i = 0; i < tab.attachedFiles.Count; i++)
            {
                GUILayout.BeginHorizontal();
                tab.attachedFiles[i] = EditorGUILayout.ObjectField(tab.attachedFiles[i], typeof(Object), false);
                if (GUILayout.Button("Kaldır", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    tab.attachedFiles.RemoveAt(i);
                    i--; // Listedeyi dengelemek için
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndVertical();

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();

        // İşlem devam ederken butonu pasif yaparak stabiliteyi sağlıyoruz
        EditorGUI.BeginDisabledGroup(tab.isProcessing);
        if (GUILayout.Button(tab.isProcessing ? "Yanıt Bekleniyor..." : "İstek Gönder", GUILayout.Height(35)))
        {
            tab.isProcessing = true;
            PostToGemini(tab);
        }

        if (GUILayout.Button("Yeniden İstek Gönder", GUILayout.Width(150), GUILayout.Height(35)))
        {
            tab.isProcessing = true;
            PostToGemini(tab);
        }
        EditorGUI.EndDisabledGroup();

        // Durdur butonu sadece işlem sürerken aktif
        EditorGUI.BeginDisabledGroup(!tab.isProcessing);
        if (GUILayout.Button("Durdur", GUILayout.Width(80), GUILayout.Height(35)))
        {
            tab.isProcessing = false;
            if (tab.activeRequest != null)
            {
                tab.activeRequest.Abort();
                tab.activeRequest = null;
            }
            tab.response = "İşlem kullanıcı tarafından durduruldu.";
            GUI.FocusControl(null);
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Hafızayı Sil", GUILayout.Width(110), GUILayout.Height(35)))
        {
            tab.chatHistory.Clear();
            tab.response = "Sohbet geçmişi temizlendi.";
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Gemini'nin Cevabı (Hafıza: {tab.chatHistory.Count} Mesaj):", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Tüm Cevabı Kopyala", GUILayout.Width(130)))
        {
            GUIUtility.systemCopyBuffer = tab.response;
        }
        GUILayout.EndHorizontal();
        
        // Kaydırma çubuğu ekleniyor ve yüksekliği sınırlandırılıyor
        tab.scrollPosition = EditorGUILayout.BeginScrollView(tab.scrollPosition, GUILayout.Height(300));
        
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        tab.response = EditorGUILayout.TextArea(tab.response, textAreaStyle, GUILayout.ExpandHeight(true));
        
        EditorGUILayout.EndScrollView();

        // Otomatik Kod Kaydetme ve Üzerine Yazma Özelliği
        if (!string.IsNullOrEmpty(tab.response) && tab.response.Contains("```csharp"))
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bu Kodu Unity'ye Aktar (Yeni Dosya Oluştur)", GUILayout.Height(35)))
            {
                ExtractAndSaveCode(tab.response);
            }

            // Sadece TEK BİR dosya ekliysek Üzerine Yazma butonunu göster (Karmaşayı önlemek ve güvenliği sağlamak için)
            if (!string.IsNullOrEmpty(tab.lastAttachedFilePath) && tab.attachedFiles.Count <= 1)
            {
                if (GUILayout.Button("Orijinal Dosyayı Güncelle (Üzerine Yaz)", GUILayout.Height(35)))
                {
                    ExtractAndOverwriteCode(tab.response, tab.lastAttachedFilePath);
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    private void TraverseHierarchy(GameObject obj, int depth, StringBuilder sb)
    {
        string indent = new string('-', depth * 2);
        sb.AppendLine($"{indent} {obj.name} (Tag: {obj.tag})");
        foreach (Transform child in obj.transform)
        {
            TraverseHierarchy(child.gameObject, depth + 1, sb);
        }
    }

    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private string BuildJsonPayload(ChatTab tab)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        
        // System Instruction (Persona & Project GDD Memory)
        string persona = "";
        if (tab.mode == PersonaMode.YazilimGelistirici) persona = "Sen süper uzman bir Unity C# yazılım geliştiricisisin. Sadece olası en temiz, optimize ve hatasız C# kodunu ve algoritmaları üret.";
        else if (tab.mode == PersonaMode.OyunTasarimcisi) persona = "Sen deneyimli ve vizyoner bir oyun tasarımcısı ve yönetmenisin. Hikaye, bölüm dizaynı, dengeleme ve kurgu fikirleri üret.";
        else if (tab.mode == PersonaMode.ShaderUzmani) persona = "Sen bir Unity Shader Graph, HLSL ve Compute Shader uzmanısın. Görsel hesaplamalar ve optimizasyon hakkında tavsiye ver.";
        
        // GDD Bağlamını Personaya Ekle (Bütün hafıza boyunca unutmayacak)
        if (!string.IsNullOrEmpty(projectGDD))
        {
            persona += "\n\nOyun Tasarım/Proje Bağlamı (Oyunun Özeti):\n" + projectGDD + "\nBana vereceğin tüm tavsiye ve kodlarda her zaman bu yukarıdaki oyun bağlamını hatırla ve buna sadık kal.";
        }

        if (!string.IsNullOrEmpty(persona))
        {
            sb.Append($"\"systemInstruction\":{{\"parts\":[{{\"text\":\"{EscapeJson(persona)}\"}}]}},");
        }

        // Web Arama Entegrasyonu
        if (tab.useWebSearch)
        {
            sb.Append("\"tools\":[{\"googleSearch\":{}}],");
        }

        sb.Append("\"contents\":[");
        for (int i = 0; i < tab.chatHistory.Count; i++)
        {
            var msg = tab.chatHistory[i];
            sb.Append($"{{\"role\":\"{msg.role}\",\"parts\":[");
            for (int p = 0; p < msg.parts.Count; p++)
            {
                var part = msg.parts[p];
                if (part.isImage)
                {
                    sb.Append($"{{\"inlineData\":{{\"mimeType\":\"image/jpeg\",\"data\":\"{part.base64Data}\"}}}}");
                }
                else if (part.isAudio)
                {
                    sb.Append($"{{\"inlineData\":{{\"mimeType\":\"audio/wav\",\"data\":\"{part.base64Data}\"}}}}");
                }
                else
                {
                    sb.Append($"{{\"text\":\"{EscapeJson(part.text)}\"}}");
                }
                if (p < msg.parts.Count - 1) sb.Append(",");
            }
            sb.Append("]}");
            if (i < tab.chatHistory.Count - 1) sb.Append(",");
        }
        sb.Append("]}");

        return sb.ToString();
    }

    private byte[] EncodeToWAV(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (var memoryStream = new MemoryStream())
        using (var binaryWriter = new BinaryWriter(memoryStream))
        {
            binaryWriter.Write(Encoding.UTF8.GetBytes("RIFF"));
            binaryWriter.Write(36 + samples.Length * 2);
            binaryWriter.Write(Encoding.UTF8.GetBytes("WAVE"));
            
            binaryWriter.Write(Encoding.UTF8.GetBytes("fmt "));
            binaryWriter.Write(16);
            binaryWriter.Write((short)1);
            binaryWriter.Write((short)clip.channels);
            binaryWriter.Write(clip.frequency);
            binaryWriter.Write(clip.frequency * clip.channels * 2);
            binaryWriter.Write((short)(clip.channels * 2));
            binaryWriter.Write((short)16);

            binaryWriter.Write(Encoding.UTF8.GetBytes("data"));
            binaryWriter.Write(samples.Length * 2);

            foreach (var sample in samples)
            {
                binaryWriter.Write((short)(sample * short.MaxValue));
            }

            return memoryStream.ToArray();
        }
    }

    private string GetLastConsoleError()
    {
        try
        {
            var logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntriesType == null) logEntriesType = System.Type.GetType("UnityEditorInternal.LogEntries, UnityEditor.dll");
            
            if (logEntriesType != null)
            {
                var getCountMethod = logEntriesType.GetMethod("GetCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (getCountMethod != null && getEntryInternalMethod != null)
                {
                    int count = (int)getCountMethod.Invoke(null, null);
                    if (count > 0)
                    {
                        var logEntryType = System.Type.GetType("UnityEditor.LogEntry, UnityEditor.dll");
                        if (logEntryType == null) logEntryType = System.Type.GetType("UnityEditorInternal.LogEntry, UnityEditor.dll");

                        object logEntry = System.Activator.CreateInstance(logEntryType);
                        var modeField = logEntryType.GetField("mode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        var messageField = logEntryType.GetField("message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        
                        for (int i = count - 1; i >= 0; i--)
                        {
                            getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                            int mode = (int)modeField.GetValue(logEntry);
                            if ((mode & (1 | 4 | 16 | 64)) != 0)
                            {
                                return (string)messageField.GetValue(logEntry);
                            }
                        }
                        return "Konsolda Hata (Error) bulunamadı.";
                    }
                }
            }
        }
        catch (System.Exception e) { Debug.LogWarning("Konsol okunamadı: " + e.Message); }
        return "Konsol bilgisine erişilemedi.";
    }

    private void ExtractAndSaveCode(string responseText)
    {
        int startIndex = responseText.IndexOf("```csharp");
        if (startIndex != -1)
        {
            startIndex += "```csharp".Length;
            int endIndex = responseText.IndexOf("```", startIndex);
            if (endIndex != -1)
            {
                string code = responseText.Substring(startIndex, endIndex - startIndex).Trim();
                string className = "NewScript";
                var match = System.Text.RegularExpressions.Regex.Match(code, @"class\s+([A-Za-z0-9_]+)");
                if (match.Success) className = match.Groups[1].Value;
                
                string path = EditorUtility.SaveFilePanelInProject("Kodu Kaydet", className + ".cs", "cs", "Komut dosyasını nereye kaydetmek istiyorsunuz?");
                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, code);
                    AssetDatabase.Refresh();
                    Debug.Log("Kod başarıyla kaydedildi: " + path);
                }
            }
        }
    }

    private void ExtractAndOverwriteCode(string responseText, string path)
    {
        int startIndex = responseText.IndexOf("```csharp");
        if (startIndex != -1)
        {
            startIndex += "```csharp".Length;
            int endIndex = responseText.IndexOf("```", startIndex);
            if (endIndex != -1)
            {
                string code = responseText.Substring(startIndex, endIndex - startIndex).Trim();
                if (File.Exists(path))
                {
                    File.WriteAllText(path, code);
                    AssetDatabase.Refresh();
                    Debug.Log("Kod başarıyla üzerine yazıldı: " + path);
                }
                else
                {
                    Debug.LogError("Dosya yolu artık geçerli değil: " + path);
                }
            }
        }
    }

    async void PostToGemini(ChatTab tab)
    {
        tab.response = "Bağlantı kuruluyor...";
        Repaint();
        
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
        string finalPrompt = tab.prompt;

        // Çoklu Dosya Okuma Sistemi
        tab.lastAttachedFilePath = ""; // Varsayılanı sıfırla (Çünkü Overwrite butonu için bilmemiz lazım)
        if (tab.attachedFiles.Count > 0)
        {
            // Sadece tek dosya varsa, orijinal dosyayı ezmeye (Overwrite) izin ver
            if (tab.attachedFiles.Count == 1 && tab.attachedFiles[0] != null)
            {
                tab.lastAttachedFilePath = AssetDatabase.GetAssetPath(tab.attachedFiles[0]);
            }

            foreach (var fileObj in tab.attachedFiles)
            {
                if (fileObj == null) continue;
                string path = AssetDatabase.GetAssetPath(fileObj);
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string fileContent = File.ReadAllText(path);
                        finalPrompt += $"\n\n--- DOSYA/KOD BAĞLAMI ({fileObj.name}) ---\n```\n{fileContent}\n```\n";
                    }
                    catch (System.Exception e) { Debug.LogError($"Dosya okunamadı ({fileObj.name}): " + e.Message); }
                }
            }
        }

        // Handle Current User Message
        ChatMessage currentMessage = new ChatMessage { role = "user" };
        
        if (!string.IsNullOrEmpty(finalPrompt))
        {
            currentMessage.parts.Add(new MessagePart { text = finalPrompt });
        }
        
        if (tab.pendingVisionBytes != null)
        {
            currentMessage.parts.Add(new MessagePart { isImage = true, base64Data = System.Convert.ToBase64String(tab.pendingVisionBytes) });
        }

        if (tab.pendingAudioBytes != null)
        {
            currentMessage.parts.Add(new MessagePart { isAudio = true, base64Data = System.Convert.ToBase64String(tab.pendingAudioBytes) });
        }

        // "Yeniden İstek Gönder" kullanıldığında eski iptal edilmiş user isteğini güncelleriz.
        if (tab.chatHistory.Count > 0 && tab.chatHistory[tab.chatHistory.Count - 1].role == "user")
        {
            tab.chatHistory[tab.chatHistory.Count - 1] = currentMessage;
        }
        else
        {
            tab.chatHistory.Add(currentMessage);
        }

        // Generate JSON String
        string jsonData = BuildJsonPayload(tab);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            tab.activeRequest = request;

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(100);
                if (!tab.isProcessing) 
                {
                    request.Abort();
                    tab.activeRequest = null;
                    return; // İptal edildi geri dön
                }
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                // İşlem başarılı tamamlandı dosya ve cacheleri temizle
                tab.attachedFiles.Clear(); 
                tab.pendingVisionBytes = null;
                tab.pendingAudioBytes = null;
                
                try 
                {
                    GeminiResponse res = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    if (res != null && res.candidates != null && res.candidates.Length > 0)
                    {
                        string responseText = res.candidates[0].content.parts[0].text;
                        tab.response = responseText;
                        
                        // Modeli belleğe kaydet
                        ChatMessage responseMsg = new ChatMessage { role = "model" };
                        responseMsg.parts.Add(new MessagePart { text = responseText });
                        tab.chatHistory.Add(responseMsg);
                    }
                    else
                    {
                        tab.response = "Yanıt parse edilemedi: " + request.downloadHandler.text;
                    }
                }
                catch
                {
                    tab.response = request.downloadHandler.text;
                }
                Debug.Log("Gemini: Başarılı!");
            }
            else
            {
                tab.response = "HATA DETAYI:\n" + request.downloadHandler.text;
            }
            tab.isProcessing = false;
            tab.activeRequest = null;
            Repaint();
        }
    }
}

[System.Serializable]
public class GeminiResponse { public Candidate[] candidates; }
[System.Serializable]
public class Candidate { public PartContent content; }
[System.Serializable]
public class PartContent { public Part[] parts; }
[System.Serializable]
public class Part { public string text; }

// Context and Serialization Structure
[System.Serializable]
public class MessagePart
{
    public string text;
    public bool isImage;
    public bool isAudio;
    public string base64Data;
}

[System.Serializable]
public class ChatMessage
{
    public string role;
    public List<MessagePart> parts = new List<MessagePart>();
}

[System.Serializable]
public class ChatTab
{
    public string tabName = "Sohbet 1";
    public string prompt = "";
    public string response = "";
    public PersonaMode mode = PersonaMode.Standart;
    public bool useWebSearch = false; // Google Web Search integration!
    
    [System.NonSerialized] public string lastAttachedFilePath = ""; // Auto Fix override
    [System.NonSerialized] public bool isProcessing = false;
    [System.NonSerialized] public UnityWebRequest activeRequest;
    
    public Vector2 scrollPosition;
    public List<Object> attachedFiles = new List<Object>(); // ÇOKLU DOSYA DESTEĞİ!
    
    [System.NonSerialized] public byte[] pendingVisionBytes = null;
    [System.NonSerialized] public byte[] pendingAudioBytes = null;

    public List<ChatMessage> chatHistory = new List<ChatMessage>();
}