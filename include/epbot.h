/*
 * EPBot Native Library — C Interface
 *
 * Bridge bidding engine by Edward Piwowar, compiled to native code via NativeAOT.
 * This header describes the C FFI exported by EPBotWrapper.dylib / .so / .dll.
 *
 * Usage:
 *   1. Create an instance per player with epbot_create()
 *   2. Initialize each player's hand with epbot_new_hand()
 *   3. Configure scoring, conventions, etc.
 *   4. Call epbot_get_bid() to get a bid, epbot_set_bid() to broadcast it
 *   5. Destroy with epbot_destroy()
 *
 * Conventions:
 *   - All functions return 0 on success, negative on error (unless documented otherwise)
 *   - Strings are UTF-8, null-terminated
 *   - String outputs are written to caller-provided buffers (buffer + buffer_size)
 *   - Array outputs are written to caller-provided buffers with a count out-param
 *   - String arrays are returned as newline-separated strings
 *   - Boolean parameters use int (0 = false, nonzero = true)
 *   - "instance" is an opaque handle from epbot_create()
 *
 * Error codes:
 *   EPBOT_OK               (0)  Success
 *   EPBOT_ERR_NULL_HANDLE  (-1) Invalid or null instance handle
 *   EPBOT_ERR_EXCEPTION    (-2) .NET exception (call epbot_get_last_error for details)
 *   EPBOT_ERR_BUFFER_SMALL (-3) Output buffer too small
 *
 * Bid encoding (used by get_bid / set_bid):
 *   0       = Pass
 *   1       = Double (X)
 *   2       = Redouble (XX)
 *   5..39   = Level/strain bids: code = 5 + (level-1)*5 + strain
 *             strain: 0=C, 1=D, 2=H, 3=S, 4=NT
 *             e.g. 1C=5, 1N=9, 2C=10, 7NT=39
 */

#ifndef EPBOT_H
#define EPBOT_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque instance handle */
typedef void* epbot_handle;

/* Error codes */
#define EPBOT_OK               0
#define EPBOT_ERR_NULL_HANDLE  (-1)
#define EPBOT_ERR_EXCEPTION    (-2)
#define EPBOT_ERR_BUFFER_SMALL (-3)

/* ======================================================================== */
/* Instance lifecycle                                                       */
/* ======================================================================== */

/* Create a new EPBot player instance. Returns handle, or NULL on failure. */
epbot_handle epbot_create(void);

/* Destroy an instance and free its resources. */
void epbot_destroy(epbot_handle instance);

/* Get the last error message (thread-local). Returns UTF-8 string or NULL. */
const char* epbot_get_last_error(void);

/* ======================================================================== */
/* Core bidding                                                             */
/* ======================================================================== */

/*
 * Initialize a player's hand.
 *   player_position: 0=N, 1=E, 2=S, 3=W
 *   longer:          newline-separated suit strings in C.D.H.S order
 *   dealer:          0=N, 1=E, 2=S, 3=W
 *   vulnerability:   0=None, 1=EW, 2=NS, 3=Both (EPBot internal encoding)
 *   repeating:       0 or 1
 *   b_playing:       0 or 1
 */
int epbot_new_hand(epbot_handle instance, int player_position, const char* longer,
                   int dealer, int vulnerability, int repeating, int b_playing);

/* Get the next bid from this player. Returns bid code (>=0) or error (<0). */
int epbot_get_bid(epbot_handle instance);

/*
 * Broadcast a bid to this player instance.
 *   spare:      position of the bidder (0-3)
 *   new_value:  bid code
 *   str_alert:  alert string (or empty "")
 */
int epbot_set_bid(epbot_handle instance, int spare, int new_value, const char* str_alert);

/* Set the bidding history as newline-separated bid strings. */
int epbot_set_arr_bids(epbot_handle instance, const char* bids);

/* Interpret a bid code (updates internal state). */
int epbot_interpret_bid(epbot_handle instance, int bid_code);

/* Ask EPBot for analysis. Returns result code. */
int epbot_ask(epbot_handle instance);

/* ======================================================================== */
/* Conventions                                                              */
/* ======================================================================== */

/* Get whether a convention is enabled. Returns 1=true, 0=false, <0=error.
 *   site: 0=NS, 1=EW */
int epbot_get_conventions(epbot_handle instance, int site, const char* convention);

/* Set a convention on/off. value: 0=off, nonzero=on. */
int epbot_set_conventions(epbot_handle instance, int site, const char* convention, int value);

int epbot_get_system_type(epbot_handle instance, int system_number);
int epbot_set_system_type(epbot_handle instance, int system_number, int value);

int epbot_get_opponent_type(epbot_handle instance, int system_number);
int epbot_set_opponent_type(epbot_handle instance, int system_number, int value);

/* Look up a convention index by name. Returns index (>=0) or <0 on error. */
int epbot_convention_index(epbot_handle instance, const char* name);

/* Get convention name by index. Writes to buffer. */
int epbot_convention_name(epbot_handle instance, int index, char* buffer, int buffer_size);
int epbot_get_convention_name(epbot_handle instance, int index, char* buffer, int buffer_size);

/* Get selected conventions as newline-separated string. count_out = number of items. */
int epbot_selected_conventions(epbot_handle instance, char* buffer, int buffer_size, int* count_out);

/* Get system name by number. */
int epbot_system_name(epbot_handle instance, int system_number, char* buffer, int buffer_size);

/* ======================================================================== */
/* Scoring & settings                                                       */
/* ======================================================================== */

/* Scoring: 0=MP, 1=IMP */
int epbot_get_scoring(epbot_handle instance);
int epbot_set_scoring(epbot_handle instance, int value);

int epbot_get_playing_skills(epbot_handle instance);
int epbot_set_playing_skills(epbot_handle instance, int value);

int epbot_get_defensive_skills(epbot_handle instance);
int epbot_set_defensive_skills(epbot_handle instance, int value);

int epbot_get_licence(epbot_handle instance);
int epbot_set_licence(epbot_handle instance, int value);

int epbot_get_bcalconsole_path(epbot_handle instance, char* buffer, int buffer_size);
int epbot_set_bcalconsole_path(epbot_handle instance, const char* path);

/* ======================================================================== */
/* State queries                                                            */
/* ======================================================================== */

int epbot_get_position(epbot_handle instance);
int epbot_get_dealer(epbot_handle instance);
int epbot_get_vulnerability(epbot_handle instance);

/* Returns EPBot version number (e.g. 8739). */
int epbot_version(epbot_handle instance);

/* Copyright string. */
int epbot_copyright(epbot_handle instance, char* buffer, int buffer_size);

/* EPBot's internal last error (distinct from FFI last error). */
int epbot_get_last_epbot_error(epbot_handle instance, char* buffer, int buffer_size);

/* Full bidding sequence as string. */
int epbot_get_str_bidding(epbot_handle instance, char* buffer, int buffer_size);

/* ======================================================================== */
/* Analysis                                                                 */
/* ======================================================================== */

/* Get probable level for a strain (0=C..4=NT). Returns level or <0 on error. */
int epbot_get_probable_level(epbot_handle instance, int strain);

/* Get probable levels for all strains. Writes int array to buffer. */
int epbot_get_probable_levels(epbot_handle instance, int* buffer, int buffer_size, int* count_out);

/*
 * Get single-dummy trick estimates.
 *   partner_longer:    newline-separated partner suit strings
 *   tricks_buffer/size/count: output for trick counts (int array)
 *   pct_buffer/size/count:    output for percentages (int array)
 */
int epbot_get_sd_tricks(epbot_handle instance, const char* partner_longer,
                        int* tricks_buffer, int tricks_buffer_size, int* tricks_count_out,
                        int* pct_buffer, int pct_buffer_size, int* pct_count_out);

/* ======================================================================== */
/* Info / meaning (bid interpretation data)                                 */
/* ======================================================================== */

int epbot_get_info_meaning(epbot_handle instance, int k, char* buffer, int buffer_size);
int epbot_set_info_meaning(epbot_handle instance, int k, const char* value);

int epbot_get_info_meaning_extended(epbot_handle instance, int position, char* buffer, int buffer_size);
int epbot_set_info_meaning_extended(epbot_handle instance, int position, const char* value);

/* Int array getters: write to buffer, set *count_out to element count. */
int epbot_get_info_feature(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_feature(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_min_length(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_min_length(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_max_length(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_max_length(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_probable_length(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_probable_length(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_honors(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_honors(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_suit_power(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_suit_power(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_strength(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_strength(epbot_handle instance, int position, const int* data, int count);

int epbot_get_info_stoppers(epbot_handle instance, int position, int* buffer, int buffer_size, int* count_out);
int epbot_set_info_stoppers(epbot_handle instance, int position, const int* data, int count);

/* Alerting flag: returns 1=alert, 0=no alert, <0=error. */
int epbot_get_info_alerting(epbot_handle instance, int k);
int epbot_set_info_alerting(epbot_handle instance, int k, int value);

int epbot_get_used_conventions(epbot_handle instance, int item);
int epbot_set_used_conventions(epbot_handle instance, int item, int value);

/* ======================================================================== */
/* Card play                                                                */
/* ======================================================================== */

/* Get opening lead suggestion. force_lead: 0 or 1. */
int epbot_get_lead(epbot_handle instance, int force_lead, char* buffer, int buffer_size);

/* Set a played card. */
int epbot_set_lead(epbot_handle instance, const char* played_card);

/*
 * Set dummy's hand.
 *   dummy:                  dummy position (0-3)
 *   arr_cards:              newline-separated card strings
 *   all_data:               0 or 1
 *   without_final_length:   pointer to byte (in/out): 0=false, 1=true
 */
int epbot_set_dummy(epbot_handle instance, int dummy, const char* arr_cards,
                    int all_data, uint8_t* without_final_length);

/* Get cards as string. */
int epbot_get_cards(epbot_handle instance, char* buffer, int buffer_size);

/* Get a player's hand. Returns newline-separated suits; *count_out = number of suits. */
int epbot_get_hand(epbot_handle instance, int player_position, char* buffer, int buffer_size, int* count_out);

/* Get suit array. current_longers: 0 or 1. */
int epbot_get_arr_suits(epbot_handle instance, int current_longers, char* buffer, int buffer_size, int* count_out);

#ifdef __cplusplus
}
#endif

#endif /* EPBOT_H */
