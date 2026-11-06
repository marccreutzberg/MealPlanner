import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Api } from '../../core/api';
import { ListStore } from '../../core/list-store';
import { MealPlan, MealPlanDay } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';

const weekday = new Intl.DateTimeFormat('da-DK', { weekday: 'long' });
const dayMonth = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'short' });

function toIso(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function parse(day: string): Date {
  const [y, m, d] = day.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** ISO 8601 week number, as Danish calendars show it. */
function isoWeek(day: string): number {
  const date = parse(day);
  date.setDate(date.getDate() + 3 - ((date.getDay() + 6) % 7));
  const firstThursday = new Date(date.getFullYear(), 0, 4);
  return 1 + Math.round(((date.getTime() - firstThursday.getTime()) / 86_400_000 - 3 + ((firstThursday.getDay() + 6) % 7)) / 7);
}

/** The week's dinners (spec 5.4). Claude plans them; here the family reads them and puts them on the list. */
@Component({
  selector: 'app-meals-page',
  imports: [TranslatePipe],
  template: `
    <header>
      <h1>{{ 'meals.title' | t }}</h1>
      <div class="week">
        <button type="button" (click)="move(-7)" [attr.aria-label]="'meals.previous' | t">‹</button>
        <span>
          <strong>{{ 'meals.week' | t: { number: weekNumber() } }}</strong>
          <span class="range">{{ range() }}</span>
        </span>
        <button type="button" (click)="move(7)" [attr.aria-label]="'meals.next' | t">›</button>
      </div>
    </header>

    <main>
      @if (plan(); as p) {
        @if (hasDishes()) {
          <ol>
            @for (day of p.days; track day.date) {
              <li [class.today]="day.date === today">
                <span class="day">{{ dayName(day) }} <span class="date">{{ dateLabel(day) }}</span></span>
                @if (day.recipe; as recipe) {
                  <details>
                    <summary>
                      <span class="dish">{{ recipe.name }}</span>
                      <span class="facts">
                        <span>{{ 'meals.minutes' | t: { count: recipe.totalMinutes } }}</span>
                        <span>{{ 'meals.servings' | t: { count: recipe.servings } }}</span>
                        @if (recipe.isChildFriendly) { <span>{{ 'meals.childFriendly' | t }}</span> }
                        @if (day.onShoppingList) { <span class="on-list">{{ 'meals.onList' | t }}</span> }
                      </span>
                    </summary>
                    <h3>{{ 'meals.ingredients' | t }}</h3>
                    <ul>
                      @for (ingredient of recipe.ingredients; track $index) {
                        <li [class.main]="ingredient.isMain">
                          @if (ingredient.quantity) { <span class="quantity">{{ ingredient.quantity }}</span> }
                          {{ ingredient.text }}
                          @if (ingredient.isMain) { <span class="visually-hidden">({{ 'meals.main' | t }})</span> }
                        </li>
                      }
                    </ul>
                    @if (recipe.method) {
                      <h3>{{ 'meals.method' | t }}</h3>
                      <p class="method">{{ recipe.method }}</p>
                    }
                  </details>
                } @else {
                  <span class="none">{{ 'meals.noDish' | t }}</span>
                }
              </li>
            }
          </ol>
          <div class="actions">
            <button class="button" type="button" [disabled]="allOnList() || busy()" (click)="toList()">{{ 'meals.toList' | t }}</button>
          </div>
        } @else {
          <p class="note">{{ 'meals.empty' | t }}</p>
        }
        <p class="note" role="status">{{ status() }}</p>
      }
    </main>
  `,
  styleUrl: './meals-page.scss',
})
export class MealsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly list = inject(ListStore);

  protected readonly today = toIso(new Date());
  protected readonly week = signal(this.today);
  protected readonly plan = signal<MealPlan | null>(null);
  protected readonly status = signal('');
  protected readonly busy = signal(false);

  protected readonly hasDishes = computed(() => this.plan()?.days.some(d => d.recipe) ?? false);
  protected readonly allOnList = computed(() => this.plan()?.days.filter(d => d.recipe).every(d => d.onShoppingList) ?? true);
  protected readonly weekNumber = computed(() => isoWeek(this.plan()?.weekStart ?? this.week()));
  protected readonly range = computed(() => {
    const p = this.plan();
    return p ? `${dayMonth.format(parse(p.weekStart))} – ${dayMonth.format(parse(p.weekEnd))}` : '';
  });

  ngOnInit(): void {
    void this.load();
  }

  protected dayName(day: MealPlanDay): string {
    const name = weekday.format(parse(day.date));
    return name.charAt(0).toUpperCase() + name.slice(1);
  }

  protected dateLabel(day: MealPlanDay): string {
    return dayMonth.format(parse(day.date));
  }

  protected move(days: number): void {
    const date = parse(this.plan()?.weekStart ?? this.week());
    date.setDate(date.getDate() + days);
    this.week.set(toIso(date));
    this.status.set('');
    void this.load();
  }

  protected async toList(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await this.api.mealPlanToList(this.week());
      this.status.set(translate('meals.added', { count: result.items.length }));
      void this.list.reload();
      await this.load();
    } catch (e) {
      this.status.set(translate(e instanceof HttpErrorResponse && e.status === 400 ? 'meals.alreadyOnList' : 'errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.plan.set(await this.api.mealPlan(this.week()));
    } catch {
      this.status.set(translate('errors.generic'));
    }
  }
}
