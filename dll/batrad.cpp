#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <deque>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "MinHook.h"

static HMODULE g_xinput = nullptr;
static FARPROC xi(const char* name) {
    if (!g_xinput) {
        wchar_t sys[MAX_PATH];
        GetSystemDirectoryW(sys, MAX_PATH);
        for (const wchar_t* n : {L"\\xinput1_4.dll", L"\\xinput9_1_0.dll"}) {
            std::wstring p = std::wstring(sys) + n;
            if ((g_xinput = LoadLibraryW(p.c_str()))) break;
        }
    }
    return g_xinput ? GetProcAddress(g_xinput, name) : nullptr;
}
#define XI_FWD(ret, name, params, args, fallback)                    \
    extern "C" ret WINAPI Proxy_##name params {                     \
        using F = ret(WINAPI*) params;                               \
        static F fwd_ = (F)xi(#name);                                   \
        return fwd_ ? fwd_ args : fallback;                                \
    }
#define XI_FWD_ORD(ret, name, ord, params, args, fallback)           \
    extern "C" ret WINAPI Proxy_##name params {                     \
        using F = ret(WINAPI*) params;                               \
        static F fwd_ = (F)xi(MAKEINTRESOURCEA(ord));                   \
        return fwd_ ? fwd_ args : fallback;                                \
    }
XI_FWD(DWORD, XInputGetState, (DWORD i, void* s), (i, s), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD(DWORD, XInputSetState, (DWORD i, void* v), (i, v), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD(DWORD, XInputGetCapabilities, (DWORD i, DWORD f, void* c), (i, f, c), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD(void, XInputEnable, (BOOL e), (e), (void)0)
XI_FWD(DWORD, XInputGetDSoundAudioDeviceGuids, (DWORD i, GUID* a, GUID* b), (i, a, b), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD(DWORD, XInputGetBatteryInformation, (DWORD i, BYTE t, void* b), (i, t, b), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD(DWORD, XInputGetKeystroke, (DWORD i, DWORD r, void* k), (i, r, k), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD_ORD(DWORD, XInputGetStateEx, 100, (DWORD i, void* s), (i, s), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD_ORD(DWORD, XInputWaitForGuideButton, 101, (DWORD i, DWORD f, void* l), (i, f, l), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD_ORD(DWORD, XInputCancelGuideButtonWait, 102, (DWORD i), (i), ERROR_DEVICE_NOT_CONNECTED)
XI_FWD_ORD(DWORD, XInputPowerOffController, 103, (DWORD i), (i), ERROR_DEVICE_NOT_CONNECTED)

static std::wstring g_dir;
static std::mutex g_logMutex;

static std::string utf8(const std::u16string& s) {
    if (s.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, (LPCWCH)s.data(), (int)s.size(), nullptr, 0, nullptr, nullptr);
    std::string out(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, (LPCWCH)s.data(), (int)s.size(), out.data(), n, nullptr, nullptr);
    return out;
}
static std::u16string utf16(const std::string& s) {
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
    std::u16string out(n, u'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), (LPWSTR)out.data(), n);
    return out;
}

static void logf(const char* fmt, ...) {
    char buf[2048];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, sizeof buf, fmt, ap);
    va_end(ap);
    SYSTEMTIME t;
    GetLocalTime(&t);
    std::lock_guard<std::mutex> lk(g_logMutex);
    FILE* f = _wfopen((g_dir + L"\\BAtrad.log").c_str(), L"ab");
    if (!f) return;
    fprintf(f, "%02d:%02d:%02d.%03d %s\r\n", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, buf);
    fclose(f);
}

static std::wstring ini(const wchar_t* key, const wchar_t* def) {
    wchar_t buf[512];
    GetPrivateProfileStringW(L"BAtrad", key, def, buf, 512, (g_dir + L"\\BAtrad.ini").c_str());
    return buf;
}
static std::string narrow(const std::wstring& w) {
    return utf8(std::u16string(w.begin(), w.end()));
}

struct Config {
    std::string assembly, klass, setter, getter;
    std::string updAssembly, updClass, updMethod;
    std::wstring from;
    int port;
    int debounceMs;
} g_cfg;

struct Il2CppObject;
struct Il2CppString;
struct Il2CppClass;
struct Il2CppImage;
struct Il2CppAssembly;
struct Il2CppDomain;
struct MethodInfo;

#define IL2CPP_API(ret, name, params) using name##_t = ret(*) params; static name##_t name = nullptr;
IL2CPP_API(Il2CppDomain*, il2cpp_domain_get, ())
IL2CPP_API(const Il2CppAssembly**, il2cpp_domain_get_assemblies, (const Il2CppDomain*, size_t*))
IL2CPP_API(const Il2CppImage*, il2cpp_assembly_get_image, (const Il2CppAssembly*))
IL2CPP_API(const char*, il2cpp_image_get_name, (const Il2CppImage*))
IL2CPP_API(size_t, il2cpp_image_get_class_count, (const Il2CppImage*))
IL2CPP_API(const Il2CppClass*, il2cpp_image_get_class, (const Il2CppImage*, size_t))
IL2CPP_API(const char*, il2cpp_class_get_name, (const Il2CppClass*))
IL2CPP_API(const MethodInfo*, il2cpp_class_get_method_from_name, (const Il2CppClass*, const char*, int))
IL2CPP_API(Il2CppString*, il2cpp_string_new_utf16, (const char16_t*, int32_t))
IL2CPP_API(char16_t*, il2cpp_string_chars, (Il2CppString*))
IL2CPP_API(int32_t, il2cpp_string_length, (Il2CppString*))
IL2CPP_API(uint32_t, il2cpp_gchandle_new_weakref, (Il2CppObject*, bool))
IL2CPP_API(Il2CppObject*, il2cpp_gchandle_get_target, (uint32_t))
IL2CPP_API(void, il2cpp_gchandle_free, (uint32_t))
IL2CPP_API(void*, il2cpp_thread_attach, (Il2CppDomain*))

static bool loadIl2cppApi(HMODULE ga) {
#define LOAD(name) if (!(name = (name##_t)GetProcAddress(ga, #name))) { logf("Export introuvable : %s", #name); return false; }
    LOAD(il2cpp_domain_get) LOAD(il2cpp_domain_get_assemblies) LOAD(il2cpp_assembly_get_image)
    LOAD(il2cpp_image_get_name) LOAD(il2cpp_image_get_class_count) LOAD(il2cpp_image_get_class)
    LOAD(il2cpp_class_get_name) LOAD(il2cpp_class_get_method_from_name) LOAD(il2cpp_string_new_utf16)
    LOAD(il2cpp_string_chars) LOAD(il2cpp_string_length) LOAD(il2cpp_gchandle_new_weakref)
    LOAD(il2cpp_gchandle_get_target) LOAD(il2cpp_gchandle_free) LOAD(il2cpp_thread_attach)
#undef LOAD
    return true;
}

static const Il2CppImage* findImage(const std::string& name) {
    size_t n = 0;
    const Il2CppAssembly** as = il2cpp_domain_get_assemblies(il2cpp_domain_get(), &n);
    for (size_t i = 0; i < n; i++) {
        const Il2CppImage* img = il2cpp_assembly_get_image(as[i]);
        if (img && name == il2cpp_image_get_name(img)) return img;
    }
    return nullptr;
}

static const Il2CppClass* findClass(const std::string& assembly, const std::string& klass) {
    const Il2CppImage* img = findImage(assembly);
    if (!img) return nullptr;
    size_t n = il2cpp_image_get_class_count(img);
    for (size_t i = 0; i < n; i++) {
        const Il2CppClass* c = il2cpp_image_get_class(img, i);
        if (c && klass == il2cpp_class_get_name(c)) return c;
    }
    return nullptr;
}

static void* methodPointer(const MethodInfo* m) { return m ? *(void* const*)m : nullptr; }

static std::u16string readString(Il2CppString* s) {
    if (!s) return {};
    return std::u16string(il2cpp_string_chars(s), il2cpp_string_length(s));
}

static std::mutex g_mutex;
static std::unordered_map<std::u16string, std::u16string> g_cache;
static std::unordered_set<std::u16string> g_outputs;
static std::unordered_map<Il2CppObject*, std::u16string> g_lastText;

struct Pending {
    uint32_t handle;
    Il2CppObject* label;
    std::u16string original;
    std::chrono::steady_clock::time_point when;
    int tries = 0;
};
struct Done {
    uint32_t handle;
    Il2CppObject* label;
    std::u16string original, translated;
};
static std::deque<Pending> g_queue;
static std::condition_variable g_queueCv;
static std::vector<Done> g_done;
static long long g_since = 0;

static bool translatable(const std::u16string& s) {
    if (s.empty() || s.size() > 2000) return false;
    for (char16_t c : s) {
        if ((c >= u'A' && c <= u'Z') || (c >= u'a' && c <= u'z')) return true;
        if (c >= 0x3040 && c <= 0x30FF) return true;
        if (c >= 0x4E00 && c <= 0x9FFF) return true;
        if (c >= 0xAC00 && c <= 0xD7AF) return true;
    }
    return false;
}

static std::string urlEncode(const std::string& s) {
    static const char* hex = "0123456789ABCDEF";
    std::string out;
    for (unsigned char c : s) {
        if (isalnum(c) || c == '-' || c == '_' || c == '.' || c == '~') out += (char)c;
        else { out += '%'; out += hex[c >> 4]; out += hex[c & 15]; }
    }
    return out;
}

static bool httpGet(const std::wstring& path, std::string& body, DWORD timeoutMs) {
    bool ok = false;
    HINTERNET s = WinHttpOpen(L"BAtrad/1.0", WINHTTP_ACCESS_TYPE_NO_PROXY, WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!s) return false;
    WinHttpSetTimeouts(s, 2000, 2000, timeoutMs, timeoutMs);
    HINTERNET c = WinHttpConnect(s, L"127.0.0.1", (INTERNET_PORT)g_cfg.port, 0);
    HINTERNET r = nullptr;
    if (c) r = WinHttpOpenRequest(c, L"GET", path.c_str(), nullptr, WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, 0);
    if (r && WinHttpSendRequest(r, WINHTTP_NO_ADDITIONAL_HEADERS, 0, WINHTTP_NO_REQUEST_DATA, 0, 0, 0) &&
        WinHttpReceiveResponse(r, nullptr)) {
        DWORD status = 0, len = sizeof status;
        WinHttpQueryHeaders(r, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, nullptr, &status, &len, nullptr);
        DWORD avail = 0;
        body.clear();
        while (WinHttpQueryDataAvailable(r, &avail) && avail) {
            std::string chunk(avail, '\0');
            DWORD read = 0;
            if (!WinHttpReadData(r, chunk.data(), avail, &read)) break;
            body.append(chunk.data(), read);
        }
        ok = status == 200;
    }
    if (r) WinHttpCloseHandle(r);
    if (c) WinHttpCloseHandle(c);
    WinHttpCloseHandle(s);
    return ok;
}

static std::wstring widen(const std::string& s) {
    std::u16string u = utf16(s);
    return std::wstring(u.begin(), u.end());
}

static std::u16string unescape(const std::string& s) {
    std::string out;
    for (size_t i = 0; i < s.size(); i++) {
        if (s[i] == '\\' && i + 1 < s.size()) {
            char n = s[++i];
            out += n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n;
        } else out += s[i];
    }
    return utf16(out);
}

static void loadServerCache() {
    std::string body;
    if (!httpGet(L"/cache?from=" + g_cfg.from, body, 30000)) {
        logf("Serveur de traduction injoignable sur le port %d (BA Traduction est-il ouvert ?)", g_cfg.port);
        return;
    }
    size_t count = 0, pos = 0;
    std::lock_guard<std::mutex> lk(g_mutex);
    while (pos < body.size()) {
        size_t eol = body.find('\n', pos);
        if (eol == std::string::npos) eol = body.size();
        std::string line = body.substr(pos, eol - pos);
        pos = eol + 1;
        size_t tab = line.find('\t');
        if (tab == std::string::npos) continue;
        std::u16string fr = unescape(line.substr(tab + 1));
        g_cache[unescape(line.substr(0, tab))] = fr;
        g_outputs.insert(fr);
        count++;
    }
    logf("Cache chargé depuis le serveur : %zu traductions", count);
}

using SetText_t = void (*)(Il2CppObject*, Il2CppString*, const MethodInfo*);
using Update_t = void (*)(Il2CppObject*, const MethodInfo*);
static SetText_t g_origSetText = nullptr;
static Update_t g_origUpdate = nullptr;
static const MethodInfo* g_setTextMethod = nullptr;
static std::atomic<bool> g_ready{false};

static void hookSetText(Il2CppObject* self, Il2CppString* value, const MethodInfo* method) {
    if (!g_ready || !value || !self) return g_origSetText(self, value, method);
    std::u16string text = readString(value);
    if (!translatable(text)) return g_origSetText(self, value, method);

    std::u16string translated;
    bool queue = false;
    {
        std::lock_guard<std::mutex> lk(g_mutex);
        if (g_outputs.count(text)) {
            g_lastText[self] = text;
            return g_origSetText(self, value, method);
        }
        g_lastText[self] = text;
        auto it = g_cache.find(text);
        if (it != g_cache.end()) translated = it->second;
        else queue = true;
    }
    if (!translated.empty()) {
        Il2CppString* fr = il2cpp_string_new_utf16(translated.data(), (int32_t)translated.size());
        return g_origSetText(self, fr, method);
    }
    if (queue) {
        uint32_t h = il2cpp_gchandle_new_weakref(self, false);
        {
            std::lock_guard<std::mutex> lk(g_mutex);
            g_queue.push_back({h, self, text, std::chrono::steady_clock::now()});
        }
        g_queueCv.notify_one();
    }
    g_origSetText(self, value, method);
}

static void hookUpdate(Il2CppObject* self, const MethodInfo* method) {
    g_origUpdate(self, method);
    std::vector<Done> batch;
    {
        std::lock_guard<std::mutex> lk(g_mutex);
        if (g_done.empty()) return;
        size_t n = min(g_done.size(), (size_t)64);
        batch.assign(g_done.begin(), g_done.begin() + n);
        g_done.erase(g_done.begin(), g_done.begin() + n);
    }
    for (Done& d : batch) {
        Il2CppObject* target = il2cpp_gchandle_get_target(d.handle);
        bool apply = false;
        if (target && !d.translated.empty()) {
            std::lock_guard<std::mutex> lk(g_mutex);
            auto it = g_lastText.find(target);
            apply = it != g_lastText.end() && it->second == d.original;
        }
        if (apply) {
            try {
                Il2CppString* fr = il2cpp_string_new_utf16(d.translated.data(), (int32_t)d.translated.size());
                g_origSetText(target, fr, g_setTextMethod);
            } catch (...) {
                logf("Exception en appliquant une traduction (libellé détruit ?)");
            }
        }
        il2cpp_gchandle_free(d.handle);
    }
}

static void pollChanges() {
    std::string body;
    if (!httpGet(L"/changes?from=" + g_cfg.from + L"&since=" + std::to_wstring(g_since), body, 5000)) return;
    size_t pos = 0, count = 0;
    std::lock_guard<std::mutex> lk(g_mutex);
    while (pos < body.size()) {
        size_t eol = body.find('\n', pos);
        if (eol == std::string::npos) eol = body.size();
        std::string line = body.substr(pos, eol - pos);
        pos = eol + 1;
        size_t tab = line.find('\t');
        if (tab == std::string::npos) continue;
        if (line.compare(0, tab, "#seq") == 0) { g_since = _atoi64(line.c_str() + tab + 1); continue; }
        std::u16string orig = unescape(line.substr(0, tab));
        std::u16string trad = unescape(line.substr(tab + 1));
        if (trad.empty()) g_cache.erase(orig);
        else { g_cache[orig] = trad; g_outputs.insert(trad); }
        count++;
    }
    if (count) logf("%zu traduction(s) corrigée(s) depuis BA Traduction", count);
}

static void worker() {
    using namespace std::chrono;
    auto prochainSondage = steady_clock::now();
    for (;;) {
        Pending p;
        bool trouve = false;
        {
            std::unique_lock<std::mutex> lk(g_mutex);
            if (g_queue.empty()) g_queueCv.wait_for(lk, seconds(2));
            auto now = steady_clock::now();
            for (auto it = g_queue.begin(); it != g_queue.end();) {
                auto last = g_lastText.find(it->label);
                if (last == g_lastText.end() || last->second != it->original) {
                    g_done.push_back({it->handle, it->label, it->original, {}});
                    it = g_queue.erase(it);
                } else ++it;
            }
            for (auto it = g_queue.rbegin(); it != g_queue.rend(); ++it) {
                if (it->when + milliseconds(g_cfg.debounceMs) <= now) {
                    p = *it;
                    g_queue.erase(std::next(it).base());
                    trouve = true;
                    break;
                }
            }
        }
        if (!trouve) {
            if (steady_clock::now() >= prochainSondage) {
                pollChanges();
                prochainSondage = steady_clock::now() + seconds(2);
            }
            bool attente;
            {
                std::lock_guard<std::mutex> lk(g_mutex);
                attente = !g_queue.empty();
            }
            if (attente) std::this_thread::sleep_for(milliseconds(40));
            continue;
        }

        Done d{p.handle, p.label, p.original, {}};
        {
            std::lock_guard<std::mutex> lk(g_mutex);
            auto c = g_cache.find(p.original);
            if (c != g_cache.end()) {
                d.translated = c->second;
                g_done.push_back(d);
                continue;
            }
        }
        std::string body;
        std::wstring path = L"/translate?from=" + g_cfg.from + L"&to=fr&text=" + widen(urlEncode(utf8(p.original)));
        bool ok = httpGet(path, body, 180000) && !body.empty();
        std::lock_guard<std::mutex> lk(g_mutex);
        if (ok) {
            d.translated = utf16(body);
            g_cache[p.original] = d.translated;
            g_outputs.insert(d.translated);
            g_done.push_back(d);
        } else if (++p.tries < 4) {
            p.when = steady_clock::now() + seconds(2 * p.tries);
            g_queue.push_front(p);
            logf("Traduction pas encore disponible (essai %d/4), nouvel essai dans %d s", p.tries, 2 * p.tries);
        } else {
            g_done.push_back(d);
            logf("Traduction abandonnée après 4 essais");
        }
    }
}

static bool installHook(void* target, void* detour, void** original, const char* what) {
    if (!target) { logf("Cible introuvable : %s", what); return false; }
    if (MH_CreateHook(target, detour, original) != MH_OK || MH_EnableHook(target) != MH_OK) {
        logf("Impossible d'intercepter %s", what);
        return false;
    }
    logf("Interception active : %s", what);
    return true;
}

static DWORD WINAPI initThread(LPVOID) {
    g_cfg.assembly = narrow(ini(L"assembly", L"BlueArchive.dll"));
    g_cfg.klass = narrow(ini(L"class", L"UILabel"));
    g_cfg.setter = narrow(ini(L"setter", L"set_text"));
    g_cfg.updAssembly = narrow(ini(L"update_assembly", L"Unity.ResourceManager.dll"));
    g_cfg.updClass = narrow(ini(L"update_class", L"MonoBehaviourCallbackHooks"));
    g_cfg.updMethod = narrow(ini(L"update_method", L"Update"));
    g_cfg.from = ini(L"from", L"en");
    g_cfg.port = _wtoi(ini(L"port", L"5050").c_str());
    g_cfg.debounceMs = _wtoi(ini(L"debounce_ms", L"800").c_str());
    logf("=== BAtrad démarré (source %s, port %d) ===", narrow(g_cfg.from).c_str(), g_cfg.port);

    HMODULE ga = nullptr;
    const Il2CppClass* label = nullptr;
    for (int i = 0; i < 600 && !label; i++) {
        if (!ga && (ga = GetModuleHandleW(L"GameAssembly.dll")) && !loadIl2cppApi(ga)) return 0;
        if (ga && il2cpp_domain_get && il2cpp_domain_get()) {
            il2cpp_thread_attach(il2cpp_domain_get());
            label = findClass(g_cfg.assembly, g_cfg.klass);
        }
        if (!label) Sleep(500);
    }
    if (!label) { logf("Classe %s introuvable dans %s", g_cfg.klass.c_str(), g_cfg.assembly.c_str()); return 0; }

    g_setTextMethod = il2cpp_class_get_method_from_name(label, g_cfg.setter.c_str(), 1);
    const Il2CppClass* upd = findClass(g_cfg.updAssembly, g_cfg.updClass);
    const MethodInfo* updMethod = upd ? il2cpp_class_get_method_from_name(upd, g_cfg.updMethod.c_str(), 0) : nullptr;

    if (MH_Initialize() != MH_OK) { logf("MinHook n'a pas pu démarrer"); return 0; }
    std::string what = g_cfg.klass + "." + g_cfg.setter;
    if (!installHook(methodPointer(g_setTextMethod), (void*)hookSetText, (void**)&g_origSetText, what.c_str())) return 0;
    what = g_cfg.updClass + "." + g_cfg.updMethod;
    if (!installHook(methodPointer(updMethod), (void*)hookUpdate, (void**)&g_origUpdate, what.c_str()))
        logf("Sans mise à jour par image : les nouveaux textes ne passeront en français qu'au prochain affichage.");

    loadServerCache();
    std::thread(worker).detach();
    g_ready = true;
    logf("Prêt.");
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(inst);
        wchar_t exe[MAX_PATH];
        GetModuleFileNameW(nullptr, exe, MAX_PATH);
        g_dir = exe;
        g_dir = g_dir.substr(0, g_dir.find_last_of(L"\\/"));

        std::wstring name = std::wstring(exe).substr(g_dir.size() + 1);
        if (_wcsicmp(name.c_str(), L"BlueArchive.exe") == 0)
            CloseHandle(CreateThread(nullptr, 0, initThread, nullptr, 0, nullptr));
    }
    return TRUE;
}
