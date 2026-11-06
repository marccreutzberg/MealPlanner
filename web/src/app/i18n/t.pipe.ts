import { Pipe, PipeTransform } from '@angular/core';
import da from './da.json';

type Params = Record<string, string | number | null | undefined>;

/** Looks up a dotted key in the Danish dictionary and fills {placeholders}. Missing keys show the key itself. */
export function translate(key: string, params?: Params): string {
  const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], da);
  const text = typeof value === 'string' ? value : key;
  return params ? text.replace(/\{(\w+)\}/g, (_, name: string) => String(params[name] ?? '')) : text;
}

@Pipe({ name: 't' })
export class TranslatePipe implements PipeTransform {
  transform(key: string, params?: Params): string {
    return translate(key, params);
  }
}
