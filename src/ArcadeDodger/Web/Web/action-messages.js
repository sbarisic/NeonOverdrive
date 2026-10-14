const reasons = {
  cache_already_searched: 'You already searched this cache during this sector.',
  close_menu_first: 'Return to flight before searching.',
  command_queue_full:
    'Too many unacknowledged commands. Nothing was charged; wait for synchronization and retry.',
  stock_reserved: 'Another pilot is purchasing this item. Wait for the stock update.',
  search_out_of_range: 'Move within 100 units to search, with a clear path to the target.',
  open_close_range_trade: 'Approach within 180 units and choose Close-range trade before buying.',
  no_eligible_target:
    'No eligible mission target is currently available. Try another assignment later.',
  no_delivery_station: 'No friendly, uncontested delivery station is available.',
  contact_required: 'Dock at a friendly station or speak to a contact that offers missions.',
  return_to_race_start: 'Approach the race start near Arrival Port.',
  race_not_ready:
    'Undock, end other interactions and towing, and remain damage-free for five seconds.',
  assignment_cooldown: 'Wait for the assignment cooldown before accepting more work.',
  undock_first: 'Undock before hailing or trading with a ship.',
  target_gone: 'That ship is no longer available. Select another detected contact.',
  out_of_sensor_range: 'The ship has left sensor range. Move closer to reopen radio.',
  npc_capacity_reached:
    'The sector is at its combat population limit. Retry after ships are destroyed.',
  socket_unavailable: 'This socket is occupied, or its site core must be activated first.',
  trade_unavailable: 'This contact has no trade service. Choose a trader.',
  unavailable:
    'This target or service is no longer available. Choose a nearby contact or another objective.',
  out_of_range: 'Move within interaction range (180 units) with a clear path to the target.',
  blocked: 'An obstacle blocks the signal. Move into clear line of sight.',
  capture_required:
    'Capture this site before building: clear its defenders and hold uncontested presence.',
  busy: 'A transaction is already being saved. Wait for its acknowledgement.',
  capacity: 'The sector is at its combat population limit. Try again after ships are destroyed.',
  insufficient_credits: 'You do not have enough credits for this purchase.',
  save_failed: 'The save failed. Nothing was charged; retry the action.',
  cooldown: 'This service is cooling down. Wait for the displayed timer.',
  stale: 'This offer changed. Review the refreshed items and try again.',
};
export function actionMessage(code) {
  return reasons[code] || code?.replaceAll('_', ' ') || '';
}
