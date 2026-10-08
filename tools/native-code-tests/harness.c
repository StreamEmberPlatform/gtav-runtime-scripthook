#include <string.h>
#include <stdint.h>
#define MS __attribute__((ms_abi))
__attribute__((visibility("hidden"))) void *g_shader_data; __attribute__((visibility("hidden"))) uint32_t g_shader_size, g_shader_type; __attribute__((visibility("hidden"))) void *g_shader_out;
void *get_shader_data(void){return g_shader_data;} uint32_t get_shader_size(void){return g_shader_size;} uint32_t get_shader_type(void){return g_shader_type;} void *get_shader_out(void){return g_shader_out;}
int g_resolve_calls, g_destroy_calls, g_reload_calls; uint32_t g_last_hash;
int get_counts(int i){return i==0?g_resolve_calls:i==1?g_destroy_calls:g_reload_calls;}
MS char *ms_strstr(const char *a, const char *b) { return strstr(a, b); }
static char fake_shader_obj[16];
MS void *fake_resolve(uint32_t hash) { g_resolve_calls++; g_last_hash = hash; return (hash & 1) ? fake_shader_obj : 0; }
MS void fake_destroy(void *s) { if (s == fake_shader_obj) g_destroy_calls++; }
MS void fake_reload(void) { g_reload_calls++; }
typedef MS int64_t (*scr_fn)(void *);
typedef MS void (*combine_fn)(void *, void *);
typedef MS void *(*shader_fn)(const char *, void *, uint32_t, uint32_t, void *);
typedef MS void (*present_fn)(void *);
int64_t call_scr(void *fn, void *thread) { return ((scr_fn)fn)(thread); }
void call_combine(void *fn, void *self, void *buf) { ((combine_fn)fn)(self, buf); }
void *call_shader(void *fn, const char *name, void *data, uint32_t size, uint32_t type, void *out) { return ((shader_fn)fn)(name, data, size, type, out); }
void call_present(void *fn) { ((present_fn)fn)((void *)0); }
