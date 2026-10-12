import { control } from './interface.js';

const destinations = [
  ['pilot', 'Pilot', 'pilot'],
  ['skills', 'Skills', 'mastery'],
  ['arsenal', 'Arsenal', 'strength'],
  ['alliances', 'Alliances', 'friends'],
  ['map', 'Map', 'map'],
  ['station', 'Station', 'station'],
];

// Each dialog owns a copy so its navigation remains inside the modal focus boundary.
export function createMenuNavigation(element, panels, navigate) {
  const buttons = [];
  for (const panel of panels) {
    const nav = element('nav', '', 'menu-navigation');
    nav.setAttribute('aria-label', 'Game menus');
    for (const [id, label, icon] of destinations) {
      const button = element('button', label);
      button.type = 'button';
      button.dataset.menu = id;
      control(button, icon);
      button.onclick = () => navigate(id);
      nav.append(button);
      buttons.push(button);
    }
    const heading = panel.querySelector('h2');
    const anchor = heading.parentElement === panel ? heading : heading.parentElement;
    anchor.after(nav);
    const workshopTabs = panel.querySelector('.workshop-tabs');
    if (workshopTabs) {
      const row = element('div', '', 'workshop-navigation');
      workshopTabs.before(row);
      row.append(nav, workshopTabs);
    }
  }
  return {
    update(active, alive, docked) {
      for (const button of buttons) {
        const selected = button.dataset.menu === active;
        button.setAttribute('aria-current', selected ? 'page' : 'false');
        button.dataset.variant = selected ? 'primary' : 'secondary';
        button.disabled =
          button.dataset.menu !== 'pilot' &&
          (!alive || (button.dataset.menu === 'station' && !docked));
        button.title =
          button.dataset.menu === 'station' && !docked
            ? 'Dock at a station to use its services'
            : button.textContent;
      }
    },
  };
}
